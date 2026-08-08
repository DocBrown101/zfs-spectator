using System.Diagnostics;
using System.Globalization;
using Zfs.Core.Models;

namespace Zfs.Core.Services;

public class SystemService() : ISystemService
{
    private readonly Lock sync = new();
    private ulong[] prevCpuJiffies = [];
    private List<NetworkInterfaceInfo>? prevNetwork;
    private long prevNetworkTimestamp;
    private List<DiskIoInfo>? prevDisks;
    private long prevDiskTimestamp;
    private string? cachedCpuTemperaturePath;

    // ── Dashboard API ────────────────────────────────────────────────────

    public async Task<DashboardData> GetDashboardDataAsync(
        IZfsService zfs,
        IReadOnlyList<(Pool Pool, ScrubInfo Scrub)> poolSnapshots,
        CancellationToken cancellationToken = default)
    {
        var systemTask = this.GetSystemInfoAsync(zfs, cancellationToken);
        var networkTask = this.GetNetworkInfoAsync(cancellationToken);
        var diskTask = GetDiskIoInfoAsync(cancellationToken);

        await Task.WhenAll(systemTask, networkTask, diskTask);

        return this.BuildDashboardData(systemTask.Result, networkTask.Result, diskTask.Result, poolSnapshots);
    }

    private DashboardData BuildDashboardData(
        SystemInfo system,
        List<NetworkInterfaceInfo> network,
        List<DiskIoInfo> disks,
        IReadOnlyList<(Pool Pool, ScrubInfo Scrub)> poolSnapshots)
    {
        var pools = poolSnapshots.Select(snapshot => snapshot.Pool).ToList();
        var timestamp = Stopwatch.GetTimestamp();

        var netRates = this.BuildNetworkRates(network, timestamp);
        var diskRates = this.BuildDiskIoRates(disks, timestamp);
        var poolDiskRates = BuildPoolDiskGroups(pools, diskRates);
        var poolScrubs = poolSnapshots.ToDictionary(snapshot => snapshot.Pool.Name, snapshot => snapshot.Scrub);

        return new DashboardData
        {
            System = system,
            NetworkRates = netRates,
            DiskIoRates = diskRates,
            PoolDiskIoRates = poolDiskRates,
            PoolScrubs = poolScrubs,
        };
    }

    // ── Disk I/O rate computation ────────────────────────────────────────

    private List<DiskIoRateInfo> BuildDiskIoRates(List<DiskIoInfo> disks, long timestamp)
    {
        List<DiskIoInfo>? prev;
        double elapsed;
        lock (this.sync)
        {
            prev = this.prevDisks;
            elapsed = prev != null ? Stopwatch.GetElapsedTime(this.prevDiskTimestamp, timestamp).TotalSeconds : 0;
            this.prevDisks = disks;
            this.prevDiskTimestamp = timestamp;
        }

        return ComputeDiskIoRates(disks, prev, elapsed);
    }

    internal static List<DiskIoRateInfo> ComputeDiskIoRates(List<DiskIoInfo> disks, List<DiskIoInfo>? prev, double elapsed)
    {
        var rates = new List<DiskIoRateInfo>();
        if (disks.Count == 0) return rates;

        foreach (var dk in disks)
        {
            var (readBps, writeBps, readLatMs, writeLatMs, utilPct) = ComputeRateDelta(dk, prev, elapsed);

            rates.Add(new DiskIoRateInfo
            {
                Device = dk.Device,
                ReadBytesPerSec = Math.Round(readBps, 1),
                WriteBytesPerSec = Math.Round(writeBps, 1),
                ReadLatencyMs = Math.Round(readLatMs, 2),
                WriteLatencyMs = Math.Round(writeLatMs, 2),
                QueueDepth = dk.IoInProgress,
                UtilizationPct = Math.Round(utilPct, 1),
            });
        }

        return rates;
    }

    private static (double ReadBps, double WriteBps, double ReadLatMs, double WriteLatMs, double UtilPct) ComputeRateDelta(
        DiskIoInfo current,
        List<DiskIoInfo>? prev,
        double elapsed)
    {
        double readBps = 0, writeBps = 0, readLatMs = 0, writeLatMs = 0, utilPct = 0;

        if (prev != null && elapsed > 0)
        {
            var p = prev.Find(d => d.Device == current.Device);
            if (p != null)
            {
                var dReads = SafeDelta(current.ReadsCompleted, p.ReadsCompleted);
                var dWrites = SafeDelta(current.WritesCompleted, p.WritesCompleted);

                readBps = SafeDelta(current.SectorsRead, p.SectorsRead) * 512.0 / elapsed;
                writeBps = SafeDelta(current.SectorsWritten, p.SectorsWritten) * 512.0 / elapsed;

                // Average latency = delta time / delta ops
                readLatMs = dReads > 0 ? SafeDelta(current.ReadTimeMs, p.ReadTimeMs) / dReads : 0;
                writeLatMs = dWrites > 0 ? SafeDelta(current.WriteTimeMs, p.WriteTimeMs) / dWrites : 0;

                // Utilization = delta io_time / wall_time (capped at 100%)
                utilPct = Math.Min(SafeDelta(current.IoTimeMs, p.IoTimeMs) / (elapsed * 1000) * 100, 100);
            }
        }

        return (readBps, writeBps, readLatMs, writeLatMs, utilPct);
    }

    // ── Network rate computation ───────────────────────────────────────────

    private List<NetworkRateInfo> BuildNetworkRates(List<NetworkInterfaceInfo> network, long timestamp)
    {
        var rates = new List<NetworkRateInfo>();

        if (network.Count == 0)
            return rates;

        List<NetworkInterfaceInfo>? prevNet;
        double elapsed;
        lock (this.sync)
        {
            prevNet = this.prevNetwork;
            elapsed = prevNet != null ? Stopwatch.GetElapsedTime(this.prevNetworkTimestamp, timestamp).TotalSeconds : 0;
            this.prevNetwork = network;
            this.prevNetworkTimestamp = timestamp;
        }

        foreach (var n in network)
        {
            double rxBps = 0, txBps = 0;

            if (prevNet != null && elapsed > 0)
            {
                var prev = prevNet.Find(p => p.Name == n.Name);
                if (prev != null)
                {
                    rxBps = SafeDelta(n.RxBytes, prev.RxBytes) / elapsed;
                    txBps = SafeDelta(n.TxBytes, prev.TxBytes) / elapsed;
                }
            }

            rates.Add(new NetworkRateInfo { Name = n.Name, RxBytesPerSec = rxBps, TxBytesPerSec = txBps });
        }

        return rates;
    }

    // ── System Info ──────────────────────────────────────────────────────

    public async Task<StaticSystemInfo> GetStaticSystemInfoAsync(
        IZfsService zfs,
        CancellationToken cancellationToken = default)
    {
        return await RunSafeAsync(async () =>
        {
            var hostnameTask = File.ReadAllTextAsync("/proc/sys/kernel/hostname", cancellationToken);
            var kernelTask = File.ReadAllTextAsync("/proc/sys/kernel/osrelease", cancellationToken);
            var cpuInfoTask = File.ReadAllLinesAsync("/proc/cpuinfo", cancellationToken);
            var zfsVersionTask = zfs.GetZfsVersionAsync(cancellationToken);

            await Task.WhenAll(hostnameTask, kernelTask, cpuInfoTask, zfsVersionTask);

            var cpuLines = cpuInfoTask.Result;
            var processor = cpuLines
                .Where(l => l.StartsWith("model name"))
                .Select(l => l[(l.IndexOf(':') + 1)..].Trim())
                .FirstOrDefault() ?? "Unknown";
            var logicalCoreCount = CountLogicalCores(cpuLines);
            var physicalCoreCount = CountPhysicalCoresFromCpuInfo(cpuLines) ?? CountPhysicalCoresFromSysfs();

            return new StaticSystemInfo
            {
                Hostname = hostnameTask.Result.Trim(),
                Kernel = kernelTask.Result.Trim(),
                ZfsVersion = zfsVersionTask.Result,
                Processor = processor,
                PhysicalCoreCount = physicalCoreCount ?? -1,
                LogicalCoreCount = logicalCoreCount ?? -1,
            };
        }, new StaticSystemInfo());
    }

    public async Task<SystemInfo> GetSystemInfoAsync(
        IZfsService zfs,
        CancellationToken cancellationToken = default)
    {
        return await RunSafeAsync(async () =>
        {
            var arcTask = zfs.GetArcStatsAsync(cancellationToken);
            var memTask = this.GetMemoryInfoAsync(cancellationToken);
            var cpuTask = this.GetCpuUsagePercentAsync(cancellationToken);
            var tempTask = this.GetCpuTemperatureCelsiusAsync(cancellationToken);
            var uptimeTask = File.ReadAllTextAsync("/proc/uptime", cancellationToken);

            await Task.WhenAll(uptimeTask, arcTask, memTask, cpuTask, tempTask);

            var uptimeSec = ParseD(uptimeTask.Result.Split(' ').ElementAtOrDefault(0));

            return new SystemInfo
            {
                Uptime = FormatUptime(uptimeSec),
                Arc = arcTask.Result,
                Memory = memTask.Result,
                CpuUsagePercent = cpuTask.Result,
                CpuTemperatureCelsius = tempTask.Result,
            };
        }, new SystemInfo { Uptime = "N/A" });
    }

    internal static async Task<T> RunSafeAsync<T>(Func<Task<T>> action, T fallback)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return fallback;
        }
    }

    private static double ParseD(string? s) => double.TryParse(s, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static string FormatUptime(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        if (time.TotalDays >= 1)
            return $"{(int)time.TotalDays}d {time.Hours}h {time.Minutes}m";
        if (time.TotalHours >= 1)
            return $"{time.Hours}h {time.Minutes}m {time.Seconds}s";
        return $"{time.Minutes}m {time.Seconds}s";
    }

    // ── CPU Topology ────────────────────────────────────────────────────

    /// <summary>
    /// Counts the logical CPUs from the <c>processor</c> entries of
    /// <c>/proc/cpuinfo</c>. Returns <c>null</c> when no entry exists.
    /// </summary>
    internal static int? CountLogicalCores(IReadOnlyList<string> cpuInfoLines)
    {
        var count = 0;
        foreach (var line in cpuInfoLines)
        {
            if (TrySplitKeyValue(line, out var key, out _) &&
                key.Equals("processor", StringComparison.OrdinalIgnoreCase))
                count++;
        }

        return count > 0 ? count : null;
    }

    /// <summary>
    /// Counts the physical CPU cores from the unique <c>(physical id, core id)</c>
    /// combinations of <c>/proc/cpuinfo</c>. Handles multi-socket systems and SMT.
    /// Returns <c>null</c> when the topology information is incomplete.
    /// </summary>
    internal static int? CountPhysicalCoresFromCpuInfo(IReadOnlyList<string> cpuInfoLines)
    {
        var pairs = new HashSet<(string Package, string CoreId)>();
        string? package = null;
        string? coreId = null;

        foreach (var line in cpuInfoLines)
        {
            if (!TrySplitKeyValue(line, out var key, out var value)) continue;

            if (key.Equals("processor", StringComparison.OrdinalIgnoreCase))
            {
                if (package != null || coreId != null)
                {
                    if (package == null || coreId == null) return null;
                    pairs.Add((package, coreId));
                }

                package = null;
                coreId = null;
            }
            else if (key.Equals("physical id", StringComparison.OrdinalIgnoreCase))
            {
                package = value;
            }
            else if (key.Equals("core id", StringComparison.OrdinalIgnoreCase))
            {
                coreId = value;
            }
        }

        if (package != null || coreId != null)
        {
            if (package == null || coreId == null) return null;
            pairs.Add((package, coreId));
        }

        return pairs.Count > 0 ? pairs.Count : null;
    }

    /// <summary>
    /// Counts the physical CPU cores from the unique
    /// <c>(physical_package_id, core_id)</c> combinations of the sysfs topology
    /// (<c>/sys/devices/system/cpu/cpu*/topology</c>). Used as a fallback when
    /// <c>/proc/cpuinfo</c> does not provide topology information (e.g. some
    /// ARM64/aarch64 systems). Returns <c>null</c> when not determinable.
    /// </summary>
    internal static int? CountPhysicalCoresFromSysfs(string cpuBasePath = "/sys/devices/system/cpu")
    {
        try
        {
            if (!Directory.Exists(cpuBasePath)) return null;

            var pairs = new HashSet<(string Package, string CoreId)>();
            foreach (var dir in Directory.EnumerateDirectories(cpuBasePath))
            {
                var dirName = Path.GetFileName(dir);
                if (!dirName.StartsWith("cpu", StringComparison.Ordinal) ||
                    !int.TryParse(dirName[3..], out _)) continue;

                var topology = Path.Combine(dir, "topology");
                var package = TryReadFirstLine(Path.Combine(topology, "physical_package_id"));
                var coreId = TryReadFirstLine(Path.Combine(topology, "core_id"));
                if (package == null || coreId == null) return null;
                pairs.Add((package, coreId));
            }

            return pairs.Count > 0 ? pairs.Count : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // ── CPU Temperature ────────────────────────────────────────────────

    private static readonly string[] CpuSensorDriverNames =
        ["coretemp", "k10temp", "zenpower", "cpu_thermal", "soc_thermal"];

    /// <summary>
    /// Reads the current CPU temperature in Celsius. The matching
    /// <c>temp*_input</c> path is discovered lazily and cached; on a missing or
    /// vanished sensor the discovery is retried on the next poll.
    /// </summary>
    private Task<double?> GetCpuTemperatureCelsiusAsync(CancellationToken cancellationToken)
        => RunSafeAsync(async () =>
        {
            string? path;
            lock (this.sync)
            {
                path = this.cachedCpuTemperaturePath;
            }

            if (path == null)
            {
                var discovered = FindCpuTemperatureInputPath();
                lock (this.sync)
                {
                    this.cachedCpuTemperaturePath = discovered;
                    path = discovered;
                }
            }

            if (path == null) return null;

            var celsius = TryReadCelsius(path);
            if (celsius == null)
            {
                lock (this.sync)
                {
                    this.cachedCpuTemperaturePath = null;
                }

                return null;
            }

            return celsius;
        }, null);

    private static string? FindCpuTemperatureInputPath()
        => FindCpuTemperatureInputPath("/sys/class/hwmon", "/sys/class/thermal");

    /// <summary>
    /// Discovers the <c>temp*_input</c> file of the best CPU temperature sensor.
    /// Prefers a package/Tctl/Tdie reading, then CPU core sensors, then matching
    /// thermal zones. Never throws.
    /// </summary>
    internal static string? FindCpuTemperatureInputPath(string hwmonBasePath, string thermalBasePath)
    {
        try
        {
            string? bestPath = null;
            var bestRank = 0;
            var bestValue = long.MinValue;

            if (Directory.Exists(hwmonBasePath))
            {
                foreach (var hwmonDir in Directory.EnumerateDirectories(hwmonBasePath, "hwmon*"))
                {
                    var driver = TryReadFirstLine(Path.Combine(hwmonDir, "name"));
                    if (driver == null || !CpuSensorDriverNames.Contains(driver, StringComparer.OrdinalIgnoreCase))
                        continue;

                    foreach (var inputPath in Directory.EnumerateFiles(hwmonDir, "temp*_input"))
                    {
                        if (TryReadMilliCelsius(inputPath) is not { } value) continue;
                        var rank = ClassifySensorLabel(TryReadLabel(inputPath));
                        if (rank == 0) continue;
                        if (rank > bestRank || (rank == bestRank && value > bestValue))
                        {
                            bestPath = inputPath;
                            bestRank = rank;
                            bestValue = value;
                        }
                    }
                }
            }

            if (bestPath != null) return bestPath;

            if (Directory.Exists(thermalBasePath))
            {
                string? bestZonePath = null;
                var bestZoneValue = long.MinValue;

                foreach (var zoneDir in Directory.EnumerateDirectories(thermalBasePath, "thermal_zone*"))
                {
                    var type = TryReadFirstLine(Path.Combine(zoneDir, "type"));
                    if (type == null || !IsCpuThermalZoneType(type)) continue;

                    var tempPath = Path.Combine(zoneDir, "temp");
                    if (TryReadMilliCelsius(tempPath) is not { } value) continue;
                    if (value > bestZoneValue)
                    {
                        bestZonePath = tempPath;
                        bestZoneValue = value;
                    }
                }

                if (bestZonePath != null) return bestZonePath;
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a <c>temp*_input</c> value in millidegrees Celsius, or <c>null</c>
    /// when the file is missing, unreadable or contains an implausible value.
    /// </summary>
    private static long? TryReadMilliCelsius(string inputPath)
    {
        var text = TryReadFirstLine(inputPath);
        if (text == null) return null;

        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milli))
            return null;

        // Plausible range: 0 °C .. 200 °C in millidegrees.
        return milli is > 0 and < 200_000 ? milli : null;
    }

    /// <summary>
    /// Reads a <c>temp*_input</c> file and converts the value to Celsius.
    /// Returns <c>null</c> when unavailable.
    /// </summary>
    internal static double? TryReadCelsius(string inputPath)
    {
        var milli = TryReadMilliCelsius(inputPath);
        return milli == null ? null : milli / 1000.0;
    }

    /// <summary>Ranks a sensor label: 3 = package, 2 = core, 1 = unlabeled CPU sensor, 0 = unrelated.</summary>
    private static int ClassifySensorLabel(string? label)
    {
        if (label == null) return 1;
        if (IsPackageLabel(label)) return 3;
        if (IsCoreLabel(label)) return 2;
        return 0;
    }

    private static bool IsPackageLabel(string label) =>
        label.Contains("package", StringComparison.OrdinalIgnoreCase) ||
        label.Contains("tctl", StringComparison.OrdinalIgnoreCase) ||
        label.Contains("tdie", StringComparison.OrdinalIgnoreCase) ||
        label.Equals("cpu", StringComparison.OrdinalIgnoreCase);

    private static bool IsCoreLabel(string label) =>
        label.Contains("core", StringComparison.OrdinalIgnoreCase) ||
        label.Contains("tccd", StringComparison.OrdinalIgnoreCase);

    private static bool IsCpuThermalZoneType(string type) =>
        type.Contains("cpu", StringComparison.OrdinalIgnoreCase) ||
        type.Contains("package", StringComparison.OrdinalIgnoreCase) ||
        type.Contains("pkg_temp", StringComparison.OrdinalIgnoreCase) ||
        (type.Contains("soc", StringComparison.OrdinalIgnoreCase) &&
         type.Contains("thermal", StringComparison.OrdinalIgnoreCase));

    private static string? TryReadLabel(string inputPath)
    {
        var fileName = Path.GetFileName(inputPath);
        if (!fileName.EndsWith("_input", StringComparison.Ordinal)) return null;

        var labelPath = Path.Combine(
            Path.GetDirectoryName(inputPath) ?? "",
            fileName[..^"_input".Length] + "_label");
        return TryReadFirstLine(labelPath);
    }

    private static bool TrySplitKeyValue(string line, out string key, out string value)
    {
        key = "";
        value = "";
        var colonIdx = line.IndexOf(':');
        if (colonIdx < 0) return false;

        key = line[..colonIdx].Trim();
        value = line[(colonIdx + 1)..].Trim();
        return key.Length > 0;
    }

    private static string? TryReadFirstLine(string path)
    {
        try
        {
            var line = File.ReadAllText(path).Split('\n')[0].Trim();
            return line.Length > 0 ? line : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // ── CPU Usage ────────────────────────────────────────────────────────

    private Task<double> GetCpuUsagePercentAsync(CancellationToken cancellationToken)
        => RunSafeAsync(async () =>
        {
            var lines = await File.ReadAllLinesAsync("/proc/stat", cancellationToken);
            var line = lines.FirstOrDefault(l => l.StartsWith("cpu "));
            if (line == null) return 0;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 8) return 0;

            var current = parts[1..8].Select(ulong.Parse).ToArray();

            lock (this.sync)
            {
                var prev = this.prevCpuJiffies;
                this.prevCpuJiffies = current;

                if (prev.Length < 7) return 0;

                ulong total = 0, idle = 0;
                for (var i = 0; i < 7; i++)
                {
                    var d = current[i] - prev[i];
                    total += d;
                    if (i is 3 or 4) idle += d; // idle + iowait
                }

                return total == 0 ? 0 : (double)(total - idle) / total * 100;
            }
        }, 0);

    // ── Memory Info ──────────────────────────────────────────────────────

    private Task<MemoryInfo> GetMemoryInfoAsync(CancellationToken cancellationToken)
        => RunSafeAsync(async () =>
        {
            var lines = await File.ReadAllLinesAsync("/proc/meminfo", cancellationToken);
            var values = new Dictionary<string, ulong>();

            foreach (var line in lines)
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx < 0) continue;
                var key = line[..colonIdx].Trim();
                var valStr = line[(colonIdx + 1)..].Trim().Replace(" kB", "");
                if (ulong.TryParse(valStr, out var val))
                    values[key] = val * 1024; // kB to bytes
            }

            var total = values.GetValueOrDefault("MemTotal");
            var available = values.GetValueOrDefault("MemAvailable");
            var buffers = values.GetValueOrDefault("Buffers");
            var cached = values.GetValueOrDefault("Cached");
            var swapTotal = values.GetValueOrDefault("SwapTotal");
            var swapFree = values.GetValueOrDefault("SwapFree");

            return new MemoryInfo
            {
                Total = total,
                Available = available,
                Used = total >= available ? total - available : 0,
                Buffers = buffers,
                Cached = cached,
                SwapTotal = swapTotal,
                SwapUsed = swapTotal >= swapFree ? swapTotal - swapFree : 0,
            };
        }, new MemoryInfo());

    // ── Network Info ─────────────────────────────────────────────────────

    private Task<List<NetworkInterfaceInfo>> GetNetworkInfoAsync(CancellationToken cancellationToken)
        => RunSafeAsync(async () =>
        {
            var lines = await File.ReadAllLinesAsync("/proc/net/dev", cancellationToken);
            var interfaces = new List<NetworkInterfaceInfo>();

            foreach (var line in lines)
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx < 0) continue;
                var name = line[..colonIdx].Trim();
                if (name is "Inter-" or "face") continue;

                var parts = line[(colonIdx + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 16) continue;

                // Skip loopback and inactive interfaces
                if (name == "lo") continue;

                interfaces.Add(new NetworkInterfaceInfo
                {
                    Name = name,
                    RxBytes = ParseU(parts[0]),
                    TxBytes = ParseU(parts[8]),
                });
            }
            return interfaces;
        }, []);

    // ── Disk I/O Info ────────────────────────────────────────────────────

    private static Task<List<DiskIoInfo>> GetDiskIoInfoAsync(CancellationToken cancellationToken)
        => RunSafeAsync(async () =>
        {
            var lines = await File.ReadAllLinesAsync("/proc/diskstats", cancellationToken);
            var disks = new List<DiskIoInfo>();

            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 14) continue;

                var device = parts[2];

                // Only show whole disks (sd*, nvme*n*, vd*) — skip partitions and virtual devices
                if (!IsPhysicalDisk(device)) continue;

                disks.Add(new DiskIoInfo
                {
                    Device = device,
                    ReadsCompleted = ParseU(parts[3]),
                    SectorsRead = ParseU(parts[5]),
                    ReadTimeMs = ParseU(parts[6]),
                    WritesCompleted = ParseU(parts[7]),
                    SectorsWritten = ParseU(parts[9]),
                    WriteTimeMs = ParseU(parts[10]),
                    IoInProgress = ParseU(parts[11]),
                    IoTimeMs = ParseU(parts[12]),
                });
            }
            return disks.OrderBy(d => d.Device).ToList();
        }, []);

    // ── Pool-to-Disk Mapping ────────────────────────────────────────────

    private static List<PoolDiskIoGroup> BuildPoolDiskGroups(List<Pool> pools, List<DiskIoRateInfo> diskRates)
    {
        if (pools.Count == 0 || diskRates.Count == 0) return [];

        var ratesByDevice = diskRates.ToDictionary(d => d.Device);
        var result = new List<PoolDiskIoGroup>();

        foreach (var pool in pools)
        {
            var allDevices = pool.DataDevices
                .Concat(pool.CacheDevices)
                .Concat(pool.LogDevices)
                .Concat(pool.SpecialDevices);

            var matched = new List<DiskIoRateInfo>();
            foreach (var dev in allDevices)
            {
                var baseDisk = ResolveToPhysicalDisk(dev.Path);
                if (baseDisk != null && ratesByDevice.TryGetValue(baseDisk, out var rate))
                    matched.Add(rate with { VdevType = dev.VdevType });
            }

            result.Add(new PoolDiskIoGroup { PoolName = pool.Name, Disks = matched.OrderBy(d => d.Device).ToList() });
        }

        return result;
    }

    /// <summary>
    /// Resolves a pool device path (e.g. <c>/dev/disk/by-id/wwn-xxx-part2</c>) to
    /// the physical disk name from <c>/proc/diskstats</c> (e.g. <c>sda</c>).
    /// Returns <c>null</c> if the path cannot be resolved.
    /// </summary>
    public static string? ResolveToPhysicalDisk(string devicePath)
    {
        try
        {
            // Resolve symlink to actual block device (e.g. /dev/sda2)
            var target = File.ResolveLinkTarget(devicePath, returnFinalTarget: true);
            var resolved = target?.FullName ?? devicePath;
            var name = Path.GetFileName(resolved);
            return StripPartition(name);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Strips the partition suffix from a block device name.
    /// <c>sda2</c> → <c>sda</c>, <c>nvme0n1p1</c> → <c>nvme0n1</c>.
    /// </summary>
    public static string StripPartition(string deviceName)
    {
        // NVMe: partition suffix is always pN (e.g. nvme0n1p1 → nvme0n1)
        // Without 'p' suffix the name is already a whole disk (nvme0n1)
        if (deviceName.StartsWith("nvme"))
        {
            var pIdx = deviceName.LastIndexOf('p');
            if (pIdx > 0 && pIdx < deviceName.Length - 1 && char.IsDigit(deviceName[pIdx + 1]))
                return deviceName[..pIdx];
            return deviceName;
        }

        // SCSI/SATA/virtio: sda2 → sda, vdb1 → vdb, xvda1 → xvda
        var end = deviceName.Length;
        while (end > 0 && char.IsDigit(deviceName[end - 1]))
            end--;
        return end > 0 && end < deviceName.Length ? deviceName[..end] : deviceName;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    public static bool IsPhysicalDisk(string device)
    {
        // sd[a-z]+ (SCSI/SATA), nvme[0-9]n[0-9] (NVMe), vd[a-z]+ (virtio), xvd[a-z]+ (Xen)
        if (device.StartsWith("sd") && device.Length >= 3 && device[2..].All(char.IsLetter)) return true;
        if (device.StartsWith("nvme") && device.Contains('n') && !device.Contains('p')) return true;
        if (device.StartsWith("vd") && device.Length >= 3 && device[2..].All(char.IsLetter)) return true;
        if (device.StartsWith("xvd") && device.Length >= 4 && device[3..].All(char.IsLetter)) return true;
        return false;
    }

    private static double SafeDelta(ulong current, ulong previous) => current >= previous ? current - previous : 0;

    private static ulong ParseU(string s) => ulong.TryParse(s, out var v) ? v : 0;
}

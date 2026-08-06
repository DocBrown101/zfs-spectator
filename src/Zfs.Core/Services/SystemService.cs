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

    // ── Dashboard API ────────────────────────────────────────────────────

    public async Task<DashboardData> GetDashboardDataAsync(
        IZfsService zfs,
        IZpoolService zpool,
        CancellationToken cancellationToken = default)
    {
        var poolsTask = zpool.GetAllPoolsWithScrubAsync(cancellationToken);
        var systemTask = this.GetSystemInfoAsync(zfs, cancellationToken);
        var networkTask = this.GetNetworkInfoAsync(cancellationToken);
        var diskTask = GetDiskIoInfoAsync(cancellationToken);

        await Task.WhenAll(poolsTask, systemTask, networkTask, diskTask);
        return this.BuildDashboardData(systemTask.Result, networkTask.Result, diskTask.Result, poolsTask.Result);
    }

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
            double readBps = 0, writeBps = 0, readOps = 0, writeOps = 0;
            double readLatMs = 0, writeLatMs = 0, utilPct = 0;

            if (prev != null && elapsed > 0)
            {
                var p = prev.Find(d => d.Device == dk.Device);
                if (p != null)
                {
                    var dReads = SafeDelta(dk.ReadsCompleted, p.ReadsCompleted);
                    var dWrites = SafeDelta(dk.WritesCompleted, p.WritesCompleted);

                    readBps = SafeDelta(dk.SectorsRead, p.SectorsRead) * 512.0 / elapsed;
                    writeBps = SafeDelta(dk.SectorsWritten, p.SectorsWritten) * 512.0 / elapsed;
                    readOps = dReads / elapsed;
                    writeOps = dWrites / elapsed;

                    // Average latency = delta time / delta ops
                    readLatMs = dReads > 0 ? SafeDelta(dk.ReadTimeMs, p.ReadTimeMs) / dReads : 0;
                    writeLatMs = dWrites > 0 ? SafeDelta(dk.WriteTimeMs, p.WriteTimeMs) / dWrites : 0;

                    // Utilization = delta io_time / wall_time (capped at 100%)
                    utilPct = Math.Min(SafeDelta(dk.IoTimeMs, p.IoTimeMs) / (elapsed * 1000) * 100, 100);
                }
            }

            rates.Add(new DiskIoRateInfo
            {
                Device = dk.Device,
                ReadBytesPerSec = Math.Round(readBps, 1),
                WriteBytesPerSec = Math.Round(writeBps, 1),
                ReadOpsPerSec = Math.Round(readOps, 1),
                WriteOpsPerSec = Math.Round(writeOps, 1),
                ReadLatencyMs = Math.Round(readLatMs, 2),
                WriteLatencyMs = Math.Round(writeLatMs, 2),
                QueueDepth = dk.IoInProgress,
                UtilizationPct = Math.Round(utilPct, 1),
            });
        }

        return rates;
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
        try
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
            var cpuCount = cpuLines.Count(l => l.StartsWith("processor\t"));

            return new StaticSystemInfo
            {
                Hostname = hostnameTask.Result.Trim(),
                Kernel = kernelTask.Result.Trim(),
                ZfsVersion = zfsVersionTask.Result,
                Processor = processor,
                CpuCount = cpuCount > 0 ? cpuCount : 1,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new StaticSystemInfo();
        }
    }

    public async Task<SystemInfo> GetSystemInfoAsync(
        IZfsService zfs,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var arcTask = zfs.GetArcStatsAsync(cancellationToken);
            var memTask = this.GetMemoryInfoAsync(cancellationToken);
            var cpuTask = this.GetCpuUsagePercentAsync(cancellationToken);
            var uptimeTask = File.ReadAllTextAsync("/proc/uptime", cancellationToken);

            await Task.WhenAll(uptimeTask, arcTask, memTask, cpuTask);

            var uptimeSec = ParseD(uptimeTask.Result.Split(' ').ElementAtOrDefault(0));

            return new SystemInfo
            {
                Uptime = FormatUptime(uptimeSec),
                Arc = arcTask.Result,
                Memory = memTask.Result,
                CpuUsagePercent = cpuTask.Result,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new SystemInfo { Uptime = "N/A", Arc = new(), Memory = new(), CpuUsagePercent = 0 };
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

    // ── CPU Usage ────────────────────────────────────────────────────────

    private async Task<double> GetCpuUsagePercentAsync(CancellationToken cancellationToken)
    {
        try
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
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    // ── Memory Info ──────────────────────────────────────────────────────

    private async Task<MemoryInfo> GetMemoryInfoAsync(CancellationToken cancellationToken)
    {
        try
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
                SwapFree = swapFree,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new MemoryInfo();
        }
    }

    // ── Network Info ─────────────────────────────────────────────────────

    private async Task<List<NetworkInterfaceInfo>> GetNetworkInfoAsync(CancellationToken cancellationToken)
    {
        try
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
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return [];
        }
    }

    // ── Disk I/O Info ────────────────────────────────────────────────────

    private static async Task<List<DiskIoInfo>> GetDiskIoInfoAsync(CancellationToken cancellationToken)
    {
        try
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
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return [];
        }
    }

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

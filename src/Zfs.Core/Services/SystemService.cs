using System.Globalization;
using System.Net;
using System.Text;
using Zfs.Core.Models;

namespace Zfs.Core.Services;

public class SystemService()
{
    private readonly Lock sync = new();
    private ulong[] prevCpuJiffies = [];
    private List<NetworkInterfaceInfo>? prevNetwork;
    private DateTime prevNetworkTime;
    private List<DiskIoInfo>? prevDisks;
    private DateTime prevDiskTime;
    private Dictionary<string, PoolIoSnapshot>? prevPoolIo;
    private DateTime prevPoolIoTime;

    // ── Dashboard API ────────────────────────────────────────────────────

    public async Task<DashboardData> GetDashboardDataAsync(IZfsService zfs, IZpoolService zpool)
    {
        var systemTask = this.GetSystemInfoAsync();
        var memoryTask = this.GetMemoryInfoAsync();
        var networkTask = this.GetNetworkInfoAsync();
        var diskTask = this.GetDiskIoInfoAsync();
        var arcTask = zfs.GetArcStatsAsync();
        var poolIoTask = this.GetAllPoolIoAsync(zpool);
        var cpuTask = this.GetCpuUsagePercentAsync();

        await Task.WhenAll(systemTask, memoryTask, networkTask, diskTask, arcTask, poolIoTask, cpuTask);

        var sys = systemTask.Result;
        var mem = memoryTask.Result;
        var arc = arcTask.Result;
        var cpu = cpuTask.Result;
        var network = networkTask.Result;
        var disks = diskTask.Result;
        var poolIo = poolIoTask.Result;
        var now = DateTime.UtcNow;

        // ── Text fields (set via textContent) ────────────────────────────
        var text = new Dictionary<string, string>
        {
            ["sysHostname"] = sys.Hostname,
            ["sysKernel"] = $"Linux-{sys.Kernel}",
            ["sysProcessor"] = sys.Processor,
            ["cpuUsage"] = $"{cpu:F1}%",
            ["cpuCount"] = sys.CpuCount.ToString(),
            ["sysUptime"] = sys.Uptime,
            ["memTotal"] = mem.Total.FormatBytes(),
            ["memAvail"] = mem.Available.FormatBytes(),
            ["memUsed"] = mem.Used.FormatBytes(),
            ["memPct"] = $"{mem.UsagePercent:F1} %",
            ["memBuffersCached"] = $"{mem.Buffers.FormatBytes()} / {mem.Cached.FormatBytes()}",
            ["memArc"] = arc.Size.FormatBytes(),
            ["swapUsed"] = $"{mem.SwapUsed.FormatBytes()} / {mem.SwapTotal.FormatBytes()}",
            ["swapPct"] = $"{mem.SwapUsagePercent:F1} %",
        };

        // ── HTML fields (set via innerHTML) ──────────────────────────────
        var html = new Dictionary<string, string>();

        if (arc.MaxSize > 0)
        {
            text["arcSize"] = $"{arc.Size.FormatBytes()} / {arc.MaxSize.FormatBytes()}";
            text["arcMeta"] = arc.MetadataSize.FormatBytes();
            text["arcData"] = arc.DataSize.FormatBytes();
            text["arcMruMfu"] = $"{arc.MruSize.FormatBytes()} / {arc.MfuSize.FormatBytes()}";

            var hitClass = arc.HitRate >= 90 ? "text-success" : arc.HitRate >= 70 ? "text-warning" : "text-danger";
            html["arcHitRate"] = $"<span class=\"{hitClass}\">{arc.HitRate:F1}%</span>";

            if (arc.L2Size > 0)
            {
                var l2Class = arc.L2HitRate >= 70 ? "text-success" : "text-warning";
                html["l2HitRate"] = $"<span class=\"{l2Class}\">{arc.L2HitRate:F1}% ({arc.L2Size.FormatBytes()})</span>";
            }
        }

        html["poolIoBody"] = BuildPoolIoHtml(poolIo);
        var (netHtml, netRates) = this.BuildNetworkData(network, now);
        html["netBody"] = netHtml;
        var (diskHtml, diskRates) = this.BuildDiskIoData(disks, now);
        html["diskBody"] = diskHtml;

        return new DashboardData { Text = text, Html = html, NetworkRates = netRates, DiskIoRates = diskRates };
    }

    // ── Table HTML builders ──────────────────────────────────────────────

    private static string BuildPoolIoHtml(List<IoStats> poolIo)
    {
        if (poolIo.Count == 0)
            return "<tr><td colspan=\"5\" class=\"text-body-secondary\">No pools</td></tr>";

        var sb = new StringBuilder();
        foreach (var p in poolIo)
        {
            sb.Append("<tr class=\"border-bottom border-secondary\">")
              .Append($"<td class=\"fw-semibold\">{Enc(p.PoolName)}</td>")
              .Append($"<td class=\"text-end font-monospace\">{Enc(p.ReadOps)}</td>")
              .Append($"<td class=\"text-end font-monospace\">{Enc(p.WriteOps)}</td>")
              .Append($"<td class=\"text-end font-monospace\">{Enc(p.ReadBw)}</td>")
              .Append($"<td class=\"text-end font-monospace\">{Enc(p.WriteBw)}</td>")
              .Append("</tr>");
        }
        return sb.ToString();
    }

    private (string Html, List<NetworkRateInfo> Rates) BuildNetworkData(List<NetworkInterfaceInfo> network, DateTime now)
    {
        var rates = new List<NetworkRateInfo>();

        if (network.Count == 0)
            return ("<tr><td colspan=\"3\" class=\"text-body-secondary\">No active interfaces</td></tr>", rates);

        List<NetworkInterfaceInfo>? prevNet;
        double elapsed;
        lock (this.sync)
        {
            prevNet = this.prevNetwork;
            elapsed = prevNet != null ? (now - this.prevNetworkTime).TotalSeconds : 0;
            this.prevNetwork = network;
            this.prevNetworkTime = now;
        }

        var sb = new StringBuilder();

        foreach (var n in network)
        {
            var rxRate = "\u2013";
            var txRate = "\u2013";
            double rxBps = 0, txBps = 0;

            if (prevNet != null && elapsed > 0)
            {
                var prev = prevNet.Find(p => p.Name == n.Name);
                if (prev != null)
                {
                    var rxDelta = SafeDelta(n.RxBytes, prev.RxBytes);
                    var txDelta = SafeDelta(n.TxBytes, prev.TxBytes);
                    rxBps = rxDelta / elapsed;
                    txBps = txDelta / elapsed;
                    rxRate = rxBps.FormatRate();
                    txRate = txBps.FormatRate();
                }
            }

            rates.Add(new NetworkRateInfo { Name = n.Name, RxBytesPerSec = rxBps, TxBytesPerSec = txBps });

            sb.Append("<tr class=\"border-bottom border-secondary\">")
              .Append($"<td class=\"fw-semibold\">{Enc(n.Name)}</td>")
              .Append($"<td class=\"text-end font-monospace\">{rxRate}</td>")
              .Append($"<td class=\"text-end font-monospace\">{txRate}</td>")
              .Append("</tr>");
        }

        return (sb.ToString(), rates);
    }

    private (string Html, List<DiskIoRateInfo> Rates) BuildDiskIoData(List<DiskIoInfo> disks, DateTime now)
    {
        var rates = new List<DiskIoRateInfo>();

        if (disks.Count == 0)
            return ("<tr><td colspan=\"6\" class=\"text-body-secondary\">No disks found</td></tr>", rates);

        List<DiskIoInfo>? prev;
        double elapsed;
        lock (this.sync)
        {
            prev = this.prevDisks;
            elapsed = prev != null ? (now - this.prevDiskTime).TotalSeconds : 0;
            this.prevDisks = disks;
            this.prevDiskTime = now;
        }

        var sb = new StringBuilder();
        foreach (var dk in disks)
        {
            double readBps = 0, writeBps = 0, readOps = 0, writeOps = 0;

            if (prev != null && elapsed > 0)
            {
                var p = prev.Find(d => d.Device == dk.Device);
                if (p != null)
                {
                    readBps = SafeDelta(dk.SectorsRead, p.SectorsRead) * 512.0 / elapsed;
                    writeBps = SafeDelta(dk.SectorsWritten, p.SectorsWritten) * 512.0 / elapsed;
                    readOps = SafeDelta(dk.ReadsCompleted, p.ReadsCompleted) / elapsed;
                    writeOps = SafeDelta(dk.WritesCompleted, p.WritesCompleted) / elapsed;
                }
            }

            rates.Add(new DiskIoRateInfo
            {
                Device = dk.Device,
                ReadBytesPerSec = readBps,
                WriteBytesPerSec = writeBps,
                ReadOpsPerSec = readOps,
                WriteOpsPerSec = writeOps,
                IoInProgress = dk.IoInProgress,
            });

            sb.Append("<tr class=\"border-bottom border-secondary\">")
              .Append($"<td class=\"fw-semibold font-monospace\">{Enc(dk.Device)}</td>")
              .Append($"<td class=\"text-end font-monospace\">{FormatExtensions.FormatNumber(dk.ReadsCompleted)}</td>")
              .Append($"<td class=\"text-end font-monospace\">{FormatExtensions.FormatNumber(dk.WritesCompleted)}</td>")
              .Append($"<td class=\"text-end font-monospace\">{(dk.SectorsRead * 512UL).FormatBytes()}</td>")
              .Append($"<td class=\"text-end font-monospace\">{(dk.SectorsWritten * 512UL).FormatBytes()}</td>")
              .Append($"<td class=\"text-end font-monospace\">{dk.IoInProgress}</td>")
              .Append("</tr>");
        }
        return (sb.ToString(), rates);
    }

    private static string Enc(string s) => WebUtility.HtmlEncode(s);

    // ── System Info ──────────────────────────────────────────────────────

    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        try
        {
            var uptimeTask = File.ReadAllTextAsync("/proc/uptime");
            var hostnameTask = File.ReadAllTextAsync("/proc/sys/kernel/hostname");
            var kernelTask = File.ReadAllTextAsync("/proc/sys/kernel/osrelease");
            var cpuInfoTask = File.ReadAllLinesAsync("/proc/cpuinfo");

            await Task.WhenAll(uptimeTask, hostnameTask, kernelTask, cpuInfoTask);

            var uptimeParts = uptimeTask.Result.Split(' ');

            var uptimeSec = ParseD(uptimeParts.ElementAtOrDefault(0));
            var cpuLines = cpuInfoTask.Result;
            var processor = cpuLines
                .Where(l => l.StartsWith("model name"))
                .Select(l => l[(l.IndexOf(':') + 1)..].Trim())
                .FirstOrDefault() ?? "Unknown";
            var cpuCount = cpuLines.Count(l => l.StartsWith("processor\t"));

            return new SystemInfo
            {
                Hostname = hostnameTask.Result.Trim(),
                Kernel = kernelTask.Result.Trim(),
                Processor = processor,
                CpuCount = cpuCount > 0 ? cpuCount : 1,
                Uptime = uptimeSec.FormatUptime(),
            };
        }
        catch (Exception)
        {
            return new SystemInfo { Hostname = "unknown", Kernel = "unknown", Processor = "unknown", CpuCount = 1, Uptime = "N/A" };
        }
    }

    private static double ParseD(string? s) => double.TryParse(s, CultureInfo.InvariantCulture, out var v) ? v : 0;

    // ── CPU Usage ────────────────────────────────────────────────────────

    public async Task<double> GetCpuUsagePercentAsync()
    {
        try
        {
            var lines = await File.ReadAllLinesAsync("/proc/stat");
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
        catch (Exception)
        {
            return 0;
        }
    }

    // ── Memory Info ──────────────────────────────────────────────────────

    public async Task<MemoryInfo> GetMemoryInfoAsync()
    {
        try
        {
            var lines = await File.ReadAllLinesAsync("/proc/meminfo");
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
        catch (Exception)
        {
            return new MemoryInfo();
        }
    }

    // ── Network Info ─────────────────────────────────────────────────────

    private async Task<List<NetworkInterfaceInfo>> GetNetworkInfoAsync()
    {
        try
        {
            var lines = await File.ReadAllLinesAsync("/proc/net/dev");
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
        catch (Exception)
        {
            return [];
        }
    }

    // ── Disk I/O Info ────────────────────────────────────────────────────

    private async Task<List<DiskIoInfo>> GetDiskIoInfoAsync()
    {
        try
        {
            var lines = await File.ReadAllLinesAsync("/proc/diskstats");
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
        catch (Exception)
        {
            return [];
        }
    }

    // ── Pool I/O ─────────────────────────────────────────────────────────

    private async Task<List<IoStats>> GetAllPoolIoAsync(IZpoolService zpool)
    {
        // Delta rates are computed from previous cumulative snapshot.
        var snapshots = await zpool.GetAllPoolIoSnapshotsAsync();
        var now = DateTime.UtcNow;

        Dictionary<string, PoolIoSnapshot>? prevIo;
        double elapsed;
        lock (this.sync)
        {
            prevIo = this.prevPoolIo;
            elapsed = prevIo != null ? (now - this.prevPoolIoTime).TotalSeconds : 0;
            this.prevPoolIo = snapshots.ToDictionary(s => s.Name, s => s);
            this.prevPoolIoTime = now;
        }

        var result = new List<IoStats>();

        foreach (var snap in snapshots)
        {
            double readOpsRate = 0, writeOpsRate = 0, readBwRate = 0, writeBwRate = 0;

            if (prevIo != null && elapsed > 0 &&
                prevIo.TryGetValue(snap.Name, out var prev))
            {
                readOpsRate = SafeDelta(snap.ReadOps, prev.ReadOps) / elapsed;
                writeOpsRate = SafeDelta(snap.WriteOps, prev.WriteOps) / elapsed;
                readBwRate = SafeDelta(snap.ReadBytes, prev.ReadBytes) / elapsed;
                writeBwRate = SafeDelta(snap.WriteBytes, prev.WriteBytes) / elapsed;
            }

            result.Add(new IoStats
            {
                PoolName = snap.Name,
                ReadOps = FormatExtensions.FormatOpsRate(readOpsRate),
                WriteOps = FormatExtensions.FormatOpsRate(writeOpsRate),
                ReadBw = readBwRate.FormatRate(),
                WriteBw = writeBwRate.FormatRate(),
            });
        }

        return result;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static bool IsPhysicalDisk(string device)
    {
        // sd[a-z] (SCSI/SATA), nvme[0-9]n[0-9] (NVMe), vd[a-z] (virtio)
        if (device.StartsWith("sd") && device.Length == 3 && char.IsLetter(device[2])) return true;
        if (device.StartsWith("nvme") && device.Contains('n') && !device.Contains('p')) return true;
        if (device.StartsWith("vd") && device.Length == 3 && char.IsLetter(device[2])) return true;
        if (device.StartsWith("xvd") && device.Length == 4 && char.IsLetter(device[3])) return true;
        return false;
    }

    private static double SafeDelta(ulong current, ulong previous) => current >= previous ? current - previous : 0;

    private static ulong ParseU(string s) => ulong.TryParse(s, out var v) ? v : 0;
}

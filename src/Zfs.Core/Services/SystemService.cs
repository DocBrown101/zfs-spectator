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

    // ── Dashboard API ────────────────────────────────────────────────────

    public async Task<DashboardData> GetDashboardDataAsync(IZfsService zfs, IZpoolService zpool)
    {
        var systemTask = this.GetSystemInfoAsync();
        var memoryTask = this.GetMemoryInfoAsync();
        var networkTask = this.GetNetworkInfoAsync();
        var vdevDataTask = zpool.GetAllPoolsVdevDataAsync();
        var arcTask = zfs.GetArcStatsAsync();
        var cpuTask = this.GetCpuUsagePercentAsync();

        await Task.WhenAll(systemTask, memoryTask, networkTask, vdevDataTask, arcTask, cpuTask);

        var sys = systemTask.Result;
        var mem = memoryTask.Result;
        var arc = arcTask.Result;
        var cpu = cpuTask.Result;
        var network = networkTask.Result;
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

        var (netHtml, netRates) = this.BuildNetworkData(network, now);
        html["netBody"] = netHtml;

        return new DashboardData { Text = text, Html = html, NetworkRates = netRates, PoolLatencies = vdevDataTask.Result };
    }

    // ── Table HTML builders ──────────────────────────────────────────────

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

    // ── Helpers ──────────────────────────────────────────────────────────

    private static double SafeDelta(ulong current, ulong previous) => current >= previous ? current - previous : 0;

    private static ulong ParseU(string s) => ulong.TryParse(s, out var v) ? v : 0;
}

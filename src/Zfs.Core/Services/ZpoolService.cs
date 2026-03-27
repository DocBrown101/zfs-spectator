using System.Text.Json;
using Zfs.Core.Models;
using Zfs.Core.Services.Parser;

namespace Zfs.Core.Services;

public class ZpoolService(ICommandExecutor cmd) : IZpoolService
{
    // ── Pool name cache (kept warm by ListPoolsAsync) ─────────────────────

    private volatile IReadOnlyList<string>? cachedPoolNames;

    // ── VDEV delta state ──────────────────────────────────────────────────

    private readonly Lock vdevLock = new();
    private Dictionary<string, VdevCumulativeSnapshot>? prevVdevData;
    private DateTime prevVdevTime;

    // ── Pools ─────────────────────────────────────────────────────────────

    public async Task<List<Pool>> GetAllPoolsAsync()
    {
        var pools = await this.ListPoolsAsync();
        var result = new List<Pool>(pools.Count);
        foreach (var pool in pools)
        {
            var (enriched, _) = await this.EnrichPoolAsync(pool);
            result.Add(enriched);
        }
        return result;
    }

    public async Task<List<string>> GetPoolNamesAsync()
    {
        var snapshot = this.cachedPoolNames;
        if (snapshot == null)
        {
            _ = await this.ListPoolsAsync();
            snapshot = this.cachedPoolNames;
        }

        return snapshot?.ToList() ?? [];
    }

    public async Task<Pool?> GetPoolByNameAsync(string name)
    {
        var pools = await this.ListPoolsAsync();
        var pool = pools.FirstOrDefault(p => p.Name == name);
        if (pool == null) return null;
        var (enriched, _) = await this.EnrichPoolAsync(pool);
        return enriched;
    }

    public async Task<(Pool Pool, ScrubInfo Scrub)?> GetPoolWithScrubAsync(string name)
    {
        var pools = await this.ListPoolsAsync();
        var pool = pools.FirstOrDefault(p => p.Name == name);
        if (pool == null) return null;
        var (enriched, scrub) = await this.EnrichPoolAsync(pool);

        if (scrub.State == "running")
        {
            var text = await cmd.ExecuteAsync("zpool", $"status {name}");
            var timeLeft = ZpoolParser.ParseScrubTimeLeft(text);
            if (!string.IsNullOrEmpty(timeLeft))
                scrub = scrub with { TimeLeft = timeLeft };
        }

        return (enriched, scrub);
    }

    private async Task<List<Pool>> ListPoolsAsync()
    {
        var json = await cmd.ExecuteAsync("zpool", "list -Hpvj -o name,size,alloc,free,health,frag");
        var pools = string.IsNullOrWhiteSpace(json) ? [] : ZpoolParser.ParsePools(json);

        this.cachedPoolNames = pools.Select(p => p.Name).ToList().AsReadOnly();

        return pools;
    }

    private async Task<(Pool Pool, ScrubInfo Scrub)> EnrichPoolAsync(Pool pool)
    {
        var propsTask = this.GetPoolPropertiesAsync(pool.Name);
        var statusJson = await cmd.ExecuteAsync("zpool", $"status -Pj {pool.Name}");

        var layout = ParsePoolLayout(statusJson, pool.Name);
        var scrub = ParseScrubInfo(statusJson, pool.Name);

        var props = await propsTask;
        var enriched = layout.ApplyTo(pool, pool.SpecialSize, pool.SpecialAlloc, pool.SpecialFree);
        return (enriched with
        {
            UsableUsed = props.UsableUsed,
            UsableAvail = props.UsableAvail,
            UsableSize = props.UsableUsed + props.UsableAvail,
            Compression = props.Compression,
            CompRatio = props.CompRatio,
            Dedup = props.Dedup,
            Sync = props.Sync,
            Atime = props.Atime,
            Ashift = await this.GetPoolAshiftAsync(pool.Name),
            Encrypted = props.Encrypted,
            KeyLocked = props.KeyLocked,
            EncryptionAlgorithm = props.EncryptionAlgorithm,
        }, scrub);
    }

    private async Task<PoolProperties> GetPoolPropertiesAsync(string poolName)
    {
        var json = await cmd.ExecuteAsync("zfs", $"get -Hpj used,available,compression,compressratio,dedup,sync,atime,encryption,keystatus {poolName}");

        if (string.IsNullOrWhiteSpace(json))
            return DefaultPoolProperties;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return DefaultPoolProperties; }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("datasets", out var datasets))
                return DefaultPoolProperties;
            if (!datasets.TryGetProperty(poolName, out var ds))
                return DefaultPoolProperties;
            if (!ds.TryGetProperty("properties", out var props))
                return DefaultPoolProperties;

            var encryption = JsonHelper.GetPropertyString(props, "encryption");
            var encrypted = encryption is not ("off" or "-" or "");
            var keystatus = JsonHelper.GetPropertyString(props, "keystatus");

            return new PoolProperties(
                UsableUsed: JsonHelper.GetPropertyUlong(props, "used"),
                UsableAvail: JsonHelper.GetPropertyUlong(props, "available"),
                Compression: DefaultIfEmpty(JsonHelper.GetPropertyString(props, "compression"), "lz4"),
                CompRatio: DefaultIfEmpty(JsonHelper.GetPropertyString(props, "compressratio"), "1.00x"),
                Dedup: DefaultIfEmpty(JsonHelper.GetPropertyString(props, "dedup"), "off"),
                Sync: DefaultIfEmpty(JsonHelper.GetPropertyString(props, "sync"), "standard"),
                Atime: DefaultIfEmpty(JsonHelper.GetPropertyString(props, "atime"), "off"),
                Encrypted: encrypted,
                KeyLocked: keystatus == "unavailable",
                EncryptionAlgorithm: encrypted ? encryption : "");
        }
    }

    private async Task<int> GetPoolAshiftAsync(string poolName)
    {
        var json = await cmd.ExecuteAsync("zpool", $"get -Hpj ashift {poolName}");
        return ZpoolParser.ParseAshift(json, poolName);
    }

    private static readonly PoolProperties DefaultPoolProperties =
        new(0, 0, "n/a", "n/a", "n/a", "n/a", "n/a", false, false, "n/a");

    private record PoolProperties(
        ulong UsableUsed, ulong UsableAvail,
        string Compression, string CompRatio, string Dedup, string Sync, string Atime,
        bool Encrypted, bool KeyLocked, string EncryptionAlgorithm);

    // ── Pool Layout & Scrub ──────────────────────────────────────────────

    private static PoolLayout ParsePoolLayout(string statusJson, string poolName)
    {
        var layout = ZpoolParser.ParsePoolLayout(statusJson, poolName);

        return layout with
        {
            DataDevices = WithPresence(layout.DataDevices),
            CacheDevices = WithPresence(layout.CacheDevices),
            LogDevices = WithPresence(layout.LogDevices),
            SpareDevices = WithPresence(layout.SpareDevices),
            SpecialDevices = WithPresence(layout.SpecialDevices),
        };
    }

    private static IReadOnlyList<PoolDevice> WithPresence(IReadOnlyList<PoolDevice> devices)
    {
        var result = new PoolDevice[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            var path = devices[i].Path;
            var present = File.Exists(path) ||
                          File.Exists(path.StartsWith('/') ? path : $"/dev/{path}");
            result[i] = devices[i] with { Present = present };
        }
        return result;
    }

    private static ScrubInfo ParseScrubInfo(string statusJson, string poolName)
    {
        return ZpoolParser.ParseScrubInfo(statusJson, poolName);
    }

    public async Task<ScrubInfo> GetScrubStatusAsync(string poolName)
    {
        var json = await cmd.ExecuteAsync("zpool", $"status -Pj {poolName}");
        var scrub = ParseScrubInfo(json, poolName);

        if (scrub.State == "running")
        {
            var text = await cmd.ExecuteAsync("zpool", $"status {poolName}");
            var timeLeft = ZpoolParser.ParseScrubTimeLeft(text);
            if (!string.IsNullOrEmpty(timeLeft))
                scrub = scrub with { TimeLeft = timeLeft };
        }

        return scrub;
    }

    // ── VDEV Data for all pools (from zpool iostat -vlHp) ──────────────

    public async Task<List<PoolLatencyData>> GetAllPoolsVdevDataAsync()
    {
        var output = await cmd.ExecuteAsync("zpool", "iostat -vlHp");
        var pools = ZpoolParser.ParseVdevIostat(output, this.cachedPoolNames);

        var snapshot = pools.SelectMany(p => p.Devices).ToDictionary(d => d.DevicePath);

        var now = DateTime.UtcNow;
        Dictionary<string, VdevCumulativeSnapshot>? prev;
        double elapsed;
        lock (this.vdevLock)
        {
            prev = this.prevVdevData;
            elapsed = prev != null ? (now - this.prevVdevTime).TotalSeconds : 0;
            this.prevVdevData = snapshot;
            this.prevVdevTime = now;
        }

        return pools.Select(pool => new PoolLatencyData
        {
            PoolName = pool.PoolName,
            Devices = pool.Devices
                .Select(d => ToLatencyInfo(d, prev?.GetValueOrDefault(d.DevicePath), elapsed))
                .ToList(),
        }).ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static VdevLatencyInfo ToLatencyInfo(VdevCumulativeSnapshot d, VdevCumulativeSnapshot? prev, double elapsed)
    {
        double readOps = 0, writeOps = 0, readBw = 0, writeBw = 0;
        double readLatMs = 0, writeLatMs = 0, queueDepth = 0, utilPct = 0;

        if (prev is not null && elapsed > 0)
        {
            var dReadOps = Math.Max(d.ReadOps - prev.ReadOps, 0);
            var dWriteOps = Math.Max(d.WriteOps - prev.WriteOps, 0);
            var dTotalR = Math.Max(d.TotalWaitReadNs - prev.TotalWaitReadNs, 0);
            var dTotalW = Math.Max(d.TotalWaitWriteNs - prev.TotalWaitWriteNs, 0);
            var dDiskR = Math.Max(d.DiskWaitReadNs - prev.DiskWaitReadNs, 0);
            var dDiskW = Math.Max(d.DiskWaitWriteNs - prev.DiskWaitWriteNs, 0);
            var wallNs = elapsed * 1_000_000_000;

            readOps = dReadOps / elapsed;
            writeOps = dWriteOps / elapsed;
            readBw = Math.Max(d.ReadBytes - prev.ReadBytes, 0) / elapsed;
            writeBw = Math.Max(d.WriteBytes - prev.WriteBytes, 0) / elapsed;
            readLatMs = dReadOps > 0 ? dTotalR / dReadOps / 1_000_000.0 : 0;
            writeLatMs = dWriteOps > 0 ? dTotalW / dWriteOps / 1_000_000.0 : 0;
            queueDepth = (dTotalR + dTotalW) / wallNs;
            utilPct = Math.Min((dDiskR + dDiskW) / wallNs * 100, 100);
        }

        return new VdevLatencyInfo
        {
            DevicePath = d.DevicePath,
            DeviceName = VdevLatencyInfo.ShortenDeviceName(Path.GetFileName(d.DevicePath)),
            Role = d.Role,
            ReadLatencyMs = Math.Round(readLatMs, 2),
            WriteLatencyMs = Math.Round(writeLatMs, 2),
            ReadOpsPerSec = Math.Round(readOps, 1),
            WriteOpsPerSec = Math.Round(writeOps, 1),
            ReadBytesPerSec = Math.Round(readBw, 1),
            WriteBytesPerSec = Math.Round(writeBw, 1),
            QueueDepth = Math.Round(queueDepth, 2),
            UtilizationPct = Math.Round(utilPct, 1),
        };
    }

    private static string DefaultIfEmpty(string value, string fallback)
        => string.IsNullOrEmpty(value) ? fallback : value;
}

using System.Text.Json;
using Zfs.Core.Models;
using Zfs.Core.Services.Parser;

namespace Zfs.Core.Services;

public class ZpoolService(ICommandExecutor cmd) : IZpoolService
{
    // ── Pool name cache (kept warm by ListPoolsAsync) ─────────────────────

    private volatile IReadOnlyList<string>? cachedPoolNames;

    // ── Pools ─────────────────────────────────────────────────────────────

    public async Task<List<Pool>> GetAllPoolsAsync()
    {
        return (await this.GetAllPoolsCoreAsync(includeScrubTimeLeft: false))
            .Select(snapshot => snapshot.Pool)
            .ToList();
    }

    public Task<List<(Pool Pool, ScrubInfo Scrub)>> GetAllPoolsWithScrubAsync()
        => this.GetAllPoolsCoreAsync(includeScrubTimeLeft: true);

    private async Task<List<(Pool Pool, ScrubInfo Scrub)>> GetAllPoolsCoreAsync(bool includeScrubTimeLeft)
    {
        var pools = await this.ListPoolsAsync();
        var result = (await Task.WhenAll(pools.Select(this.EnrichPoolAsync))).ToList();

        if (includeScrubTimeLeft)
        {
            var scrubs = await Task.WhenAll(result.Select(snapshot =>
                this.AddScrubTimeLeftAsync(snapshot.Pool.Name, snapshot.Scrub)));
            for (var i = 0; i < result.Count; i++)
                result[i] = (result[i].Pool, scrubs[i]);
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

        scrub = await this.AddScrubTimeLeftAsync(name, scrub);

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

        scrub = await this.AddScrubTimeLeftAsync(poolName, scrub);

        return scrub;
    }

    private async Task<ScrubInfo> AddScrubTimeLeftAsync(string poolName, ScrubInfo scrub)
    {
        if (scrub.State != "running") return scrub;

        var text = await cmd.ExecuteAsync("zpool", $"status {poolName}");
        var timeLeft = ZpoolParser.ParseScrubTimeLeft(text);
        return string.IsNullOrEmpty(timeLeft) ? scrub : scrub with { TimeLeft = timeLeft };
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string DefaultIfEmpty(string value, string fallback)
        => string.IsNullOrEmpty(value) ? fallback : value;
}

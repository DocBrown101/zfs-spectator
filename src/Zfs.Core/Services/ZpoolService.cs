using System.Text.Json;
using Zfs.Core.Models;
using Zfs.Core.Services.Parser;

namespace Zfs.Core.Services;

public class ZpoolService(ICommandExecutor cmd) : IZpoolService
{
    private static readonly string[] RequiredPoolProperties =
    [
        "used", "available", "compression", "compressratio", "dedup", "sync", "atime", "encryption", "keystatus",
    ];

    // ── Pool name cache (kept warm by ListPoolsAsync) ─────────────────────

    private volatile IReadOnlyList<string>? cachedPoolNames;

    // ── Pools ─────────────────────────────────────────────────────────────

    public async Task<List<Pool>> GetAllPoolsAsync()
    {
        return (await this.GetAllPoolsCoreAsync(
                includeScrubTimeLeft: false,
                requireOutput: false,
                cancellationToken: CancellationToken.None))
            .Select(snapshot => snapshot.Pool)
            .ToList();
    }

    public Task<List<(Pool Pool, ScrubInfo Scrub)>> GetAllPoolsWithScrubAsync(CancellationToken cancellationToken = default)
        => this.GetAllPoolsCoreAsync(
            includeScrubTimeLeft: true,
            requireOutput: true,
            cancellationToken: cancellationToken);

    public async Task<List<(Pool Pool, ScrubInfo Scrub)>> GetDashboardPoolsAsync(CancellationToken cancellationToken = default)
    {
        var pools = await this.ListPoolsAsync(cancellationToken, requireOutput: true);
        var result = (await Task.WhenAll(pools.Select(pool => this.GetPoolRuntimeAsync(pool, cancellationToken)))).ToList();
        var scrubs = await Task.WhenAll(result.Select(snapshot =>
            this.AddScrubTimeLeftAsync(snapshot.Pool.Name, snapshot.Scrub, cancellationToken)));

        for (var i = 0; i < result.Count; i++)
            result[i] = (result[i].Pool, scrubs[i]);

        return result;
    }

    private async Task<List<(Pool Pool, ScrubInfo Scrub)>> GetAllPoolsCoreAsync(
        bool includeScrubTimeLeft,
        bool requireOutput,
        CancellationToken cancellationToken)
    {
        var pools = await this.ListPoolsAsync(cancellationToken, requireOutput);
        var result = (await Task.WhenAll(pools.Select(pool =>
            this.EnrichPoolAsync(pool, cancellationToken, requireOutput)))).ToList();

        if (includeScrubTimeLeft)
        {
            var scrubs = await Task.WhenAll(result.Select(snapshot =>
                this.AddScrubTimeLeftAsync(snapshot.Pool.Name, snapshot.Scrub, cancellationToken)));
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
        var (enriched, _) = await this.EnrichPoolAsync(pool, CancellationToken.None);
        return enriched;
    }

    public async Task<(Pool Pool, ScrubInfo Scrub)?> GetPoolWithScrubAsync(string name)
    {
        var pools = await this.ListPoolsAsync();
        var pool = pools.FirstOrDefault(p => p.Name == name);
        if (pool == null) return null;
        var (enriched, scrub) = await this.EnrichPoolAsync(pool, CancellationToken.None);

        scrub = await this.AddScrubTimeLeftAsync(name, scrub, CancellationToken.None);

        return (enriched, scrub);
    }

    private async Task<List<Pool>> ListPoolsAsync(
        CancellationToken cancellationToken = default,
        bool requireOutput = false)
    {
        var json = await cmd.ExecuteAsync("zpool", "list -Hpvj -o name,size,alloc,free,health,frag", cancellationToken);
        if (requireOutput && !string.IsNullOrWhiteSpace(json))
            RequirePoolList(json);
        var pools = string.IsNullOrWhiteSpace(json) ? [] : ZpoolParser.ParsePools(json);

        this.cachedPoolNames = pools.Select(p => p.Name).ToList().AsReadOnly();

        return pools;
    }

    private async Task<(Pool Pool, ScrubInfo Scrub)> EnrichPoolAsync(
        Pool pool,
        CancellationToken cancellationToken,
        bool requireOutput = false)
    {
        var propsTask = this.GetPoolPropertiesAsync(pool.Name, cancellationToken, requireOutput);
        var statusTask = cmd.ExecuteAsync("zpool", $"status -Pj {pool.Name}", cancellationToken);
        await Task.WhenAll(propsTask, statusTask);

        var statusJson = await statusTask;
        if (requireOutput)
            RequirePoolStatus(statusJson, pool.Name);

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
            Ashift = await this.GetPoolAshiftAsync(pool.Name, cancellationToken, requireOutput),
            Encrypted = props.Encrypted,
            KeyLocked = props.KeyLocked,
            EncryptionAlgorithm = props.EncryptionAlgorithm,
        }, scrub);
    }

    private async Task<(Pool Pool, ScrubInfo Scrub)> GetPoolRuntimeAsync(Pool pool, CancellationToken cancellationToken)
    {
        var statusJson = await cmd.ExecuteAsync("zpool", $"status -Pj {pool.Name}", cancellationToken);
        RequirePoolStatus(statusJson, pool.Name);
        return (
            ParsePoolLayout(statusJson, pool.Name).ApplyTo(pool, pool.SpecialSize, pool.SpecialAlloc, pool.SpecialFree),
            ParseScrubInfo(statusJson, pool.Name));
    }

    private async Task<PoolProperties> GetPoolPropertiesAsync(
        string poolName,
        CancellationToken cancellationToken,
        bool requireOutput)
    {
        var json = await cmd.ExecuteAsync("zfs", $"get -Hpj used,available,compression,compressratio,dedup,sync,atime,encryption,keystatus {poolName}", cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            if (requireOutput) throw new InvalidOperationException($"zfs get returned no data for {poolName}");
            return DefaultPoolProperties;
        }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex)
        {
            if (requireOutput) throw new InvalidOperationException($"zfs get returned invalid data for {poolName}", ex);
            return DefaultPoolProperties;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("datasets", out var datasets) ||
                !datasets.TryGetProperty(poolName, out var ds) ||
                !ds.TryGetProperty("properties", out var props))
            {
                if (requireOutput) throw new InvalidOperationException($"zfs get returned incomplete data for {poolName}");
                return DefaultPoolProperties;
            }

            if (requireOutput && RequiredPoolProperties.Any(name =>
                    !props.TryGetProperty(name, out var property) ||
                    !property.TryGetProperty("value", out _)))
                throw new InvalidOperationException($"zfs get returned incomplete data for {poolName}");

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

    private async Task<int> GetPoolAshiftAsync(
        string poolName,
        CancellationToken cancellationToken,
        bool requireOutput)
    {
        var json = await cmd.ExecuteAsync("zpool", $"get -Hpj ashift {poolName}", cancellationToken);
        if (requireOutput)
        {
            RequirePoolEntry(json, "pools", poolName, "zpool get ashift");
            using var doc = JsonDocument.Parse(json);
            var pool = doc.RootElement.GetProperty("pools").GetProperty(poolName);
            if (!pool.TryGetProperty("properties", out var properties) ||
                !properties.TryGetProperty("ashift", out var ashift) ||
                !ashift.TryGetProperty("value", out _))
                throw new InvalidOperationException($"zpool get ashift returned incomplete data for {poolName}");
        }
        return ZpoolParser.ParseAshift(json, poolName);
    }

    private static void RequirePoolEntry(string json, string containerName, string poolName, string command)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException($"{command} returned no data for {poolName}");

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(containerName, out var container) ||
                container.ValueKind != JsonValueKind.Object ||
                !container.TryGetProperty(poolName, out var pool) ||
                pool.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException($"{command} returned incomplete data for {poolName}");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{command} returned invalid data for {poolName}", ex);
        }
    }

    private static void RequirePoolStatus(string json, string poolName)
    {
        RequirePoolEntry(json, "pools", poolName, "zpool status");

        using var doc = JsonDocument.Parse(json);
        var pool = doc.RootElement.GetProperty("pools").GetProperty(poolName);
        if (!pool.TryGetProperty("vdevs", out var vdevs) ||
            vdevs.ValueKind != JsonValueKind.Object ||
            !vdevs.TryGetProperty(poolName, out var rootVdev) ||
            rootVdev.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"zpool status returned incomplete data for {poolName}");
    }

    private static void RequirePoolList(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("pools", out var pools) ||
                pools.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("zpool list returned incomplete data");

            foreach (var entry in pools.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Object ||
                    string.IsNullOrEmpty(JsonHelper.GetString(entry.Value, "name")) ||
                    !entry.Value.TryGetProperty("properties", out var properties) ||
                    properties.ValueKind != JsonValueKind.Object ||
                    !TryGetPropertyValue(properties, "size", out var size) ||
                    !ulong.TryParse(size, out _) ||
                    !TryGetPropertyValue(properties, "allocated", out var allocated) ||
                    !ulong.TryParse(allocated, out _) ||
                    !TryGetPropertyValue(properties, "free", out var free) ||
                    !ulong.TryParse(free, out _) ||
                    !TryGetPropertyValue(properties, "health", out _) ||
                    !TryGetPropertyValue(properties, "fragmentation", out var fragmentation) ||
                    !int.TryParse(fragmentation, out _))
                    throw new InvalidOperationException($"zpool list returned incomplete data for {entry.Name}");
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("zpool list returned invalid data", ex);
        }
    }

    private static bool TryGetPropertyValue(JsonElement properties, string name, out string value)
    {
        value = "";
        if (!properties.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.Object ||
            !property.TryGetProperty("value", out var propertyValue) ||
            propertyValue.ValueKind != JsonValueKind.String)
            return false;

        value = propertyValue.GetString() ?? "";
        return value.Length > 0;
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

        scrub = await this.AddScrubTimeLeftAsync(poolName, scrub, CancellationToken.None);

        return scrub;
    }

    private async Task<ScrubInfo> AddScrubTimeLeftAsync(
        string poolName,
        ScrubInfo scrub,
        CancellationToken cancellationToken)
    {
        if (scrub.State != "running") return scrub;

        var text = await cmd.ExecuteAsync("zpool", $"status {poolName}", cancellationToken);
        var timeLeft = ZpoolParser.ParseScrubTimeLeft(text);
        return string.IsNullOrEmpty(timeLeft) ? scrub : scrub with { TimeLeft = timeLeft };
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string DefaultIfEmpty(string value, string fallback)
        => string.IsNullOrEmpty(value) ? fallback : value;
}

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
        var pools = await this.ListPoolsAsync();
        var snapshots = (await Task.WhenAll(pools.Select(pool =>
            this.LoadDetailedPoolAsync(pool, CancellationToken.None, requireOutput: false)))).ToList();
        return snapshots.Select(snapshot => snapshot.Pool).ToList();
    }

    public async Task<List<(Pool Pool, ScrubInfo Scrub)>> GetAllPoolsWithScrubAsync(CancellationToken cancellationToken = default)
    {
        var pools = await this.ListPoolsAsync(cancellationToken, requireOutput: true);
        var snapshots = (await Task.WhenAll(pools.Select(pool =>
            this.LoadDetailedPoolAsync(pool, cancellationToken, requireOutput: true)))).ToList();
        return await this.AttachScrubTimeLeftAsync(snapshots, cancellationToken);
    }

    public async Task<List<(Pool Pool, ScrubInfo Scrub)>> GetDashboardPoolsAsync(CancellationToken cancellationToken = default)
    {
        var pools = await this.ListPoolsAsync(cancellationToken, requireOutput: true);
        return (await Task.WhenAll(pools.Select(pool =>
            this.LoadDashboardPoolAsync(pool, cancellationToken)))).ToList();
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

    public async Task<(Pool Pool, ScrubInfo Scrub)?> GetPoolWithScrubAsync(string name)
    {
        var pools = await this.ListPoolsAsync();
        var pool = pools.FirstOrDefault(p => p.Name == name);
        if (pool == null) return null;
        var (enriched, scrub) = await this.LoadDetailedPoolAsync(pool, CancellationToken.None, requireOutput: false);

        scrub = await this.AddScrubTimeLeftAsync(name, scrub, CancellationToken.None);

        return (enriched, scrub);
    }

    private async Task<List<Pool>> ListPoolsAsync(
        CancellationToken cancellationToken = default,
        bool requireOutput = false)
    {
        var json = await cmd.ExecuteAsync("zpool", "list -Hpvj -o name,size,alloc,free,health,frag", cancellationToken);
        var pools = ZpoolParser.ParsePools(json, requireOutput);

        this.cachedPoolNames = pools.Select(p => p.Name).ToList().AsReadOnly();

        return pools;
    }

    private async Task<(Pool Pool, ScrubInfo Scrub)> LoadDetailedPoolAsync(
        Pool pool,
        CancellationToken cancellationToken,
        bool requireOutput)
    {
        var propsTask = this.GetPoolPropertiesAsync(pool.Name, cancellationToken, requireOutput);
        var statusTask = cmd.ExecuteAsync("zpool", $"status -Pj {pool.Name}", cancellationToken);
        await Task.WhenAll(propsTask, statusTask);

        var statusJson = await statusTask;
        var layout = ParsePoolLayout(statusJson, pool.Name, requireOutput);
        var scrub = ZpoolParser.ParseScrubInfo(statusJson, pool.Name);

        var enriched = layout.ApplyTo(pool, pool.SpecialSize, pool.SpecialAlloc, pool.SpecialFree);
        var props = await propsTask;
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

    private async Task<(Pool Pool, ScrubInfo Scrub)> LoadDashboardPoolAsync(
        Pool pool,
        CancellationToken cancellationToken)
    {
        var statusJson = await cmd.ExecuteAsync("zpool", $"status -Pj {pool.Name}", cancellationToken);
        var layout = ParsePoolLayout(statusJson, pool.Name, requireOutput: true);
        var scrub = ZpoolParser.ParseScrubInfo(statusJson, pool.Name);

        return (layout.ApplyTo(pool, pool.SpecialSize, pool.SpecialAlloc, pool.SpecialFree), scrub);
    }

    private async Task<List<(Pool Pool, ScrubInfo Scrub)>> AttachScrubTimeLeftAsync(
        List<(Pool Pool, ScrubInfo Scrub)> result,
        CancellationToken cancellationToken)
    {
        var scrubs = await Task.WhenAll(result.Select(snapshot =>
            this.AddScrubTimeLeftAsync(snapshot.Pool.Name, snapshot.Scrub, cancellationToken)));

        for (var i = 0; i < result.Count; i++)
            result[i] = (result[i].Pool, scrubs[i]);

        return result;
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

        try
        {
            using var pool = JsonHelper.TryGetObject(json, "datasets", poolName);
            if (pool is null || !pool.Value.TryGetProperty("properties", out var props))
            {
                if (requireOutput) throw new InvalidOperationException($"zfs get returned incomplete data for {poolName}");
                return DefaultPoolProperties;
            }

            if (requireOutput && RequiredPoolProperties.Any(name =>
                    !props.TryGetProperty(name, out var property) ||
                    !property.TryGetProperty("value", out _)))
                throw new InvalidOperationException($"zfs get returned incomplete data for {poolName}");

            var encryption = JsonHelper.GetPropertyString(props, "encryption");
            var encrypted = JsonHelper.IsEncryptionEnabled(props);

            return new PoolProperties(
                UsableUsed: JsonHelper.GetPropertyUlong(props, "used"),
                UsableAvail: JsonHelper.GetPropertyUlong(props, "available"),
                Compression: JsonHelper.GetPropertyString(props, "compression", "lz4"),
                CompRatio: JsonHelper.GetPropertyString(props, "compressratio", "1.00x"),
                Dedup: JsonHelper.GetPropertyString(props, "dedup", "off"),
                Sync: JsonHelper.GetPropertyString(props, "sync", "standard"),
                Atime: JsonHelper.GetPropertyString(props, "atime", "off"),
                Encrypted: encrypted,
                KeyLocked: JsonHelper.IsKeyLocked(props),
                EncryptionAlgorithm: encrypted ? encryption : "");
        }
        catch (JsonException ex)
        {
            if (requireOutput) throw new InvalidOperationException($"zfs get returned invalid data for {poolName}", ex);
            return DefaultPoolProperties;
        }
    }

    private async Task<int> GetPoolAshiftAsync(
        string poolName,
        CancellationToken cancellationToken,
        bool requireOutput)
    {
        var json = await cmd.ExecuteAsync("zpool", $"get -Hpj ashift {poolName}", cancellationToken);
        return ZpoolParser.ParseAshift(json, poolName, requireOutput);
    }

    private static readonly PoolProperties DefaultPoolProperties =
        new(0, 0, "n/a", "n/a", "n/a", "n/a", "n/a", false, false, "n/a");

    private record PoolProperties(
        ulong UsableUsed, ulong UsableAvail,
        string Compression, string CompRatio, string Dedup, string Sync, string Atime,
        bool Encrypted, bool KeyLocked, string EncryptionAlgorithm);

    // ── Pool Layout & Scrub ──────────────────────────────────────────────

    private static PoolLayout ParsePoolLayout(string statusJson, string poolName, bool requireOutput = false)
    {
        var layout = ZpoolParser.ParsePoolLayout(statusJson, poolName, requireOutput);

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
}

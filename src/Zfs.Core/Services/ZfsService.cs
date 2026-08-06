using Zfs.Core.Models;
using Zfs.Core.Services.Parser;

namespace Zfs.Core.Services;

public class ZfsService(ICommandExecutor cmd, IZpoolService zpoolService) : IZfsService
{
    // ── Datasets ──────────────────────────────────────────────────────────

    public async Task<List<Dataset>> GetAllDatasetsAsync()
        => (await ZfsAggregation.GetAllByPoolAsync(zpoolService, this.GetDatasetsAsync))
            .SelectMany(group => group.Items).ToList();

    public async Task<List<Dataset>> GetDatasetsAsync(string poolName)
    {
        var fields = "name,used,avail,refer,quota,refquota,compression,compressratio," +
                     "recordsize,mountpoint,sync,dedup,casesensitivity,refreservation," +
                     "zfsnas:comment,encryption,keystatus,mounted,canmount,usedbysnapshots";

        var json = await cmd.ExecuteAsync("zfs", $"list -Hpj -r -t filesystem -o {fields} {poolName}");
        return ZfsParser.ParseDatasets(json, poolName);
    }

    // ── Snapshots ─────────────────────────────────────────────────────────

    public async Task<List<Snapshot>> GetSnapshotsAsync(string poolName)
    {
        var json = await cmd.ExecuteAsync("zfs",
            $"list -Hpj -r -t snapshot -o name,used,refer,creation -s creation {poolName}");
        return ZfsParser.ParseSnapshots(json);
    }

    // ── ZVols ─────────────────────────────────────────────────────────────

    public async Task<List<ZVol>> GetAllZVolsAsync()
    {
        var fields = "name,volsize,used,refer,compression,compressratio,sync,dedup," +
                     "volblocksize,encryption,zfsnas:comment,refreservation";

        var json = await cmd.ExecuteAsync("zfs", $"list -Hpj -t volume -o {fields}");
        return ZfsParser.ParseZVols(json);
    }

    // ── ZFS Version ───────────────────────────────────────────────────────

    public async Task<string> GetZfsVersionAsync(CancellationToken cancellationToken = default)
    {
        var output = await cmd.ExecuteAsync("zfs", "version", cancellationToken);
        var match = RegexHelper.ZfsVersionRegex().Match(output);
        return match.Success ? match.Value : "unknown";
    }

    // ── ARC Statistics ─────────────────────────────────────────────────────

    public Task<ArcStats> GetArcStatsAsync(CancellationToken cancellationToken = default)
        => SystemService.RunSafeAsync(async () =>
        {
            var output = await cmd.ExecuteAsync("cat", "/proc/spl/kstat/zfs/arcstats", cancellationToken);
            var stats = new Dictionary<string, ulong>();

            foreach (var line in output.Split('\n'))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && ulong.TryParse(parts[2], out var val))
                    stats[parts[0]] = val;
            }

            return new ArcStats
            {
                Size = stats.GetValueOrDefault("size"),
                MaxSize = stats.GetValueOrDefault("c_max"),
                Hits = stats.GetValueOrDefault("hits"),
                Misses = stats.GetValueOrDefault("misses"),
                L2Hits = stats.GetValueOrDefault("l2_hits"),
                L2Misses = stats.GetValueOrDefault("l2_misses"),
                L2Size = stats.GetValueOrDefault("l2_size"),
                MruSize = stats.GetValueOrDefault("mru_size"),
                MfuSize = stats.GetValueOrDefault("mfu_size"),
                MetadataSize = stats.GetValueOrDefault("arc_meta_used"),
                DataSize = stats.GetValueOrDefault("data_size"),
            };
        }, new ArcStats());
}

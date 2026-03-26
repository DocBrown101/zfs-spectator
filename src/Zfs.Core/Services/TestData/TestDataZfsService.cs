using Zfs.Core.Models;
using Zfs.Core.Services.Parser;

namespace Zfs.Core.Services.TestData;

public class TestDataZfsService(IZpoolService zpoolService) : IZfsService
{
    public async Task<List<Dataset>> GetAllDatasetsAsync()
    {
        var names = await zpoolService.GetPoolNamesAsync();
        var tasks = names.Select(name => this.GetDatasetsAsync(name));
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).ToList();
    }

    public Task<List<Dataset>> GetDatasetsAsync(string poolName)
    {
        var json = TestDataHelper.ReadEmbeddedJson("zfs_list_datasets.json");
        return Task.FromResult(ZfsParser.ParseDatasets(json, poolName));
    }

    public Task<List<Snapshot>> GetSnapshotsAsync(string poolName)
    {
        var json = TestDataHelper.ReadEmbeddedJson("zfs_list_snapshots.json");
        return Task.FromResult(ZfsParser.ParseSnapshots(json));
    }

    public Task<List<ZVol>> GetAllZVolsAsync()
    {
        var json = TestDataHelper.ReadEmbeddedJson("zfs_list_zvols.json");
        return Task.FromResult(ZfsParser.ParseZVols(json));
    }

    public Task<string> GetZfsVersionAsync()
    {
        return Task.FromResult("2.3.1");
    }

    public Task<ArcStats> GetArcStatsAsync()
    {
        return Task.FromResult(new ArcStats
        {
            Size = 8589934592,        // 8 GiB
            MaxSize = 17179869184,    // 16 GiB
            Hits = 9500000,
            Misses = 500000,
            L2Hits = 700000,
            L2Misses = 300000,
            L2Size = 214748364800,    // 200 GiB
            MruSize = 4294967296,     // 4 GiB
            MfuSize = 3221225472,     // 3 GiB
            MetadataSize = 1073741824, // 1 GiB
            DataSize = 7516192768,    // ~7 GiB
        });
    }
}

using Zfs.Core.Models;
using Zfs.Core.Services.Parser;

namespace Zfs.Core.Services.TestData;

public class DemoDataZpoolService : IZpoolService
{
    private const string ZpoolListData = "zpool_list.json";
    private const string ZpoolStatus = "zpool_status.json";

    public async Task<List<Pool>> GetAllPoolsAsync()
        => (await this.GetAllPoolsWithScrubAsync()).Select(snapshot => snapshot.Pool).ToList();

    public Task<List<(Pool Pool, ScrubInfo Scrub)>> GetAllPoolsWithScrubAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var poolListJson = DemoDataHelper.ReadEmbeddedJson(ZpoolListData);
        var pools = ZpoolParser.ParsePools(poolListJson);

        var statusJson = DemoDataHelper.ReadEmbeddedJson(ZpoolStatus);
        var ashiftJson = DemoDataHelper.ReadEmbeddedJson("zpool_get_ashift.json");

        var result = new List<(Pool Pool, ScrubInfo Scrub)>();
        foreach (var pool in pools)
        {
            var layout = ZpoolParser.ParsePoolLayout(statusJson, pool.Name);
            var ashift = ZpoolParser.ParseAshift(ashiftJson, pool.Name);
            var scrub = ZpoolParser.ParseScrubInfo(statusJson, pool.Name);

            var enriched = layout.ApplyTo(pool, pool.SpecialSize, pool.SpecialAlloc, pool.SpecialFree) with
            {
                UsableUsed = pool.Alloc,
                UsableAvail = pool.Free,
                UsableSize = pool.Alloc + pool.Free,
                Ashift = ashift,
            };
            result.Add((enriched, scrub));
        }

        return Task.FromResult(result);
    }

    public Task<List<(Pool Pool, ScrubInfo Scrub)>> GetDashboardPoolsAsync(CancellationToken cancellationToken = default)
        => this.GetAllPoolsWithScrubAsync(cancellationToken);

    public Task<List<string>> GetPoolNamesAsync()
    {
        var json = DemoDataHelper.ReadEmbeddedJson(ZpoolListData);
        return Task.FromResult(ZpoolParser.ParsePools(json).Select(p => p.Name).ToList());
    }

    public async Task<(Pool Pool, ScrubInfo Scrub)?> GetPoolWithScrubAsync(string name)
    {
        var snapshots = await this.GetAllPoolsWithScrubAsync();
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Pool.Name == name) return snapshot;
        }
        return null;
    }

}

using Zfs.Core.Models;
using Zfs.Core.Services.Parser;

namespace Zfs.Core.Services.TestData;

public class TestDataZpoolService : IZpoolService
{
    public Task<List<Pool>> GetAllPoolsAsync()
    {
        var poolListJson = TestDataHelper.ReadEmbeddedJson("zpool_list.json");
        var pools = ZpoolParser.ParsePools(poolListJson);

        var statusJson = TestDataHelper.ReadEmbeddedJson("zpool_status.json");
        var ashiftJson = TestDataHelper.ReadEmbeddedJson("zpool_get_ashift.json");
        var vdevJson = TestDataHelper.ReadEmbeddedJson("zpool_list_vdev.json");

        var result = new List<Pool>();
        foreach (var pool in pools)
        {
            var layout = ZpoolParser.ParsePoolLayout(statusJson, pool.Name);
            var ashift = ZpoolParser.ParseAshift(ashiftJson, pool.Name);
            var (specialSize, specialAlloc, specialFree) = layout.SpecialDevices.Count > 0
                ? ZpoolParser.ParseSpecialVdevSize(vdevJson, pool.Name)
                : (0UL, 0UL, 0UL);

            result.Add(pool with
            {
                UsableUsed = pool.Alloc,
                UsableAvail = pool.Free,
                UsableSize = pool.Alloc + pool.Free,
                Compression = "lz4",
                CompRatio = "1.00x",
                Dedup = "off",
                Sync = "standard",
                Atime = "off",
                Ashift = ashift,
                VdevType = layout.VdevType,
                Operation = layout.Operation,
                DataDevices = layout.DataDevices,
                CacheDevices = layout.CacheDevices,
                LogDevices = layout.LogDevices,
                SpareDevices = layout.SpareDevices,
                SpecialDevices = layout.SpecialDevices,
                SpecialSize = specialSize,
                SpecialAlloc = specialAlloc,
                SpecialFree = specialFree,
                ErrorsRead = layout.PoolErrorsRead,
                ErrorsWrite = layout.PoolErrorsWrite,
                ErrorsChecksum = layout.PoolErrorsChecksum,
            });
        }

        return Task.FromResult(result);
    }

    public Task<List<string>> GetPoolNamesAsync()
    {
        var json = TestDataHelper.ReadEmbeddedJson("zpool_list.json");
        return Task.FromResult(ZpoolParser.ParsePoolNames(json));
    }

    public async Task<Pool?> GetPoolByNameAsync(string name)
    {
        var pools = await this.GetAllPoolsAsync();
        return pools.FirstOrDefault(p => p.Name == name);
    }

    public Task<ScrubInfo> GetScrubStatusAsync(string poolName)
    {
        var json = TestDataHelper.ReadEmbeddedJson("zpool_status.json");
        var scrub = ZpoolParser.ParseScrubInfo(json, poolName);
        return Task.FromResult(scrub);
    }

    public Task<List<PoolIoSnapshot>> GetAllPoolIoSnapshotsAsync()
    {
        var json = TestDataHelper.ReadEmbeddedJson("zpool_list.json");
        var names = ZpoolParser.ParsePoolNames(json);

        var result = names.Select(name => new PoolIoSnapshot(
            Name: name,
            ReadOps: 1024,
            WriteOps: 512,
            ReadBytes: 104857600,
            WriteBytes: 52428800
        )).ToList();

        return Task.FromResult(result);
    }
}

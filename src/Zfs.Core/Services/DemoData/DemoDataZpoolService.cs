using Zfs.Core.Models;
using Zfs.Core.Services.Parser;

namespace Zfs.Core.Services.TestData;

public class DemoDataZpoolService : IZpoolService
{
    private const string ZpoolListData = "zpool_list.json";
    private const string ZpoolStatus = "zpool_status.json";

    public Task<List<Pool>> GetAllPoolsAsync()
    {
        var poolListJson = DemoDataHelper.ReadEmbeddedJson(ZpoolListData);
        var pools = ZpoolParser.ParsePools(poolListJson);

        var statusJson = DemoDataHelper.ReadEmbeddedJson(ZpoolStatus);
        var ashiftJson = DemoDataHelper.ReadEmbeddedJson("zpool_get_ashift.json");

        var result = new List<Pool>();
        foreach (var pool in pools)
        {
            var layout = ZpoolParser.ParsePoolLayout(statusJson, pool.Name);
            var ashift = ZpoolParser.ParseAshift(ashiftJson, pool.Name);

            result.Add(layout.ApplyTo(pool, pool.SpecialSize, pool.SpecialAlloc, pool.SpecialFree) with
            {
                UsableUsed = pool.Alloc,
                UsableAvail = pool.Free,
                UsableSize = pool.Alloc + pool.Free,
                Ashift = ashift,
            });
        }

        return Task.FromResult(result);
    }

    public Task<List<string>> GetPoolNamesAsync()
    {
        var json = DemoDataHelper.ReadEmbeddedJson(ZpoolListData);
        return Task.FromResult(ZpoolParser.ParsePoolNames(json));
    }

    public async Task<Pool?> GetPoolByNameAsync(string name)
    {
        var pools = await this.GetAllPoolsAsync();
        return pools.FirstOrDefault(p => p.Name == name);
    }

    public async Task<(Pool Pool, ScrubInfo Scrub)?> GetPoolWithScrubAsync(string name)
    {
        var pool = await this.GetPoolByNameAsync(name);
        if (pool == null) return null;
        var scrub = await this.GetScrubStatusAsync(name);
        return (pool, scrub);
    }

    public Task<ScrubInfo> GetScrubStatusAsync(string poolName)
    {
        var json = DemoDataHelper.ReadEmbeddedJson(ZpoolStatus);
        var scrub = ZpoolParser.ParseScrubInfo(json, poolName);
        return Task.FromResult(scrub);
    }

    public async Task<List<PoolLatencyData>> GetAllPoolsVdevDataAsync()
    {
        var pools = await this.GetAllPoolsAsync();
        var rng = Random.Shared;
        var result = new List<PoolLatencyData>();

        foreach (var pool in pools)
        {
            var devices = new List<VdevLatencyInfo>();

            foreach (var dev in pool.DataDevices
                .Concat(pool.SpecialDevices)
                .Concat(pool.CacheDevices)
                .Concat(pool.LogDevices))
            {
                var isNvme = dev.Path.Contains("nvme", StringComparison.OrdinalIgnoreCase);
                var baseRead = isNvme ? 0.2 + rng.NextDouble() * 0.8 : 3.0 + rng.NextDouble() * 12.0;
                var baseWrite = isNvme ? 0.3 + rng.NextDouble() * 1.2 : 5.0 + rng.NextDouble() * 15.0;
                var readOps = isNvme ? 200 + rng.NextDouble() * 400 : 15 + rng.NextDouble() * 60;
                var writeOps = isNvme ? 100 + rng.NextDouble() * 300 : 8 + rng.NextDouble() * 40;

                var shortName = VdevLatencyInfo.ShortenDeviceName(Path.GetFileName(dev.Path));

                devices.Add(new VdevLatencyInfo
                {
                    DevicePath = dev.Path,
                    DeviceName = shortName,
                    Role = dev.Role,
                    ReadLatencyMs = Math.Round(baseRead, 2),
                    WriteLatencyMs = Math.Round(baseWrite, 2),
                    ReadOpsPerSec = Math.Round(readOps, 1),
                    WriteOpsPerSec = Math.Round(writeOps, 1),
                    ReadBytesPerSec = Math.Round(readOps * (isNvme ? 4096 : 131072), 1),
                    WriteBytesPerSec = Math.Round(writeOps * (isNvme ? 4096 : 131072), 1),
                    QueueDepth = Math.Round(rng.NextDouble() * (isNvme ? 12 : 4), 2),
                    UtilizationPct = Math.Round(isNvme ? 3 + rng.NextDouble() * 15 : 10 + rng.NextDouble() * 45, 1),
                });
            }

            result.Add(new PoolLatencyData { PoolName = pool.Name, Devices = devices });
        }

        return result;
    }
}

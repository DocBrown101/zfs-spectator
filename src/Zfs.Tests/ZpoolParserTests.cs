namespace Zfs.Tests;

using Zfs.Core.Services.Parser;

public class ZpoolParserTests
{
    [Fact]
    public void ParsePools_ShouldParseBothPools()
    {
        var json = File.ReadAllText("TestData/zpool_list.json");

        var pools = ZpoolParser.ParsePools(json);

        Assert.Equal(2, pools.Count);
        Assert.Contains(pools, p => p.Name == "miniTank");
        Assert.Contains(pools, p => p.Name == "zfsPool");
    }

    [Fact]
    public void ParsePools_ShouldParseMiniTankProperties()
    {
        var json = File.ReadAllText("TestData/zpool_list.json");

        var pools = ZpoolParser.ParsePools(json);
        var pool = pools.Single(p => p.Name == "miniTank");

        Assert.Equal(1992864825344UL, pool.Size);
        Assert.Equal(21095649280UL, pool.Alloc);
        Assert.Equal(1971769176064UL, pool.Free);
        Assert.Equal("ONLINE", pool.Health);
        Assert.Equal(0, pool.Fragmentation);
        Assert.Equal(0UL, pool.SpecialSize);
    }

    [Fact]
    public void ParsePools_ShouldParseZfsPoolProperties()
    {
        var json = File.ReadAllText("TestData/zpool_list.json");

        var pools = ZpoolParser.ParsePools(json);
        var pool = pools.Single(p => p.Name == "zfsPool");

        Assert.Equal(9998683865088UL, pool.Size);
        Assert.Equal(9498245939200UL, pool.Alloc);
        Assert.Equal(500437925888UL, pool.Free);
        Assert.Equal("ONLINE", pool.Health);
        Assert.Equal(0, pool.Fragmentation);
        Assert.Equal(0UL, pool.SpecialSize);
    }

    [Fact]
    public void ParsePools_ShouldReturnEmptyForEmptyInput()
    {
        Assert.Empty(ZpoolParser.ParsePools(""));
        Assert.Empty(ZpoolParser.ParsePools("{}"));
    }

    [Fact]
    public void ParsePoolNames_ShouldReturnNames()
    {
        var json = File.ReadAllText("TestData/zpool_list.json");

        var names = ZpoolParser.ParsePoolNames(json);

        Assert.Equal(2, names.Count);
        Assert.Contains("miniTank", names);
        Assert.Contains("zfsPool", names);
    }

    [Fact]
    public void ParseAshift_ShouldReturnValue()
    {
        var json = File.ReadAllText("TestData/zpool_get_ashift.json");

        var ashift = ZpoolParser.ParseAshift(json, "zfsPool");

        Assert.Equal(12, ashift);
    }

    [Fact]
    public void ParseAshift_ShouldReturnZeroForMissingPool()
    {
        var json = File.ReadAllText("TestData/zpool_get_ashift.json");

        Assert.Equal(0, ZpoolParser.ParseAshift(json, "nonexistent"));
    }

    [Fact]
    public void ParsePoolLayout_ShouldParseVdevStructure()
    {
        var json = File.ReadAllText("TestData/zpool_status.json");

        var layout = ZpoolParser.ParsePoolLayout(json, "zfsPool");

        Assert.Equal("raidz1", layout.VdevType);
        Assert.Equal("", layout.Operation);
        Assert.Equal(3, layout.DataDevices.Count);
        Assert.Equal(2, layout.SpecialDevices.Count);
        Assert.Empty(layout.CacheDevices);
        Assert.Empty(layout.LogDevices);
        Assert.Empty(layout.SpareDevices);
        Assert.Equal(0, layout.PoolErrorsRead);
        Assert.Equal(0, layout.PoolErrorsWrite);
        Assert.Equal(0, layout.PoolErrorsChecksum);
    }

    [Fact]
    public void ParsePoolLayout_ShouldParseDeviceDetails()
    {
        var json = File.ReadAllText("TestData/zpool_status.json");

        var layout = ZpoolParser.ParsePoolLayout(json, "zfsPool");

        var firstData = layout.DataDevices[0];
        Assert.Equal("/dev/disk/by-id/wwn-0x50014ee2c06fdd9f-part2", firstData.Path);
        Assert.Equal("raidz1", firstData.Role);
        Assert.Equal("ONLINE", firstData.Status);
        Assert.Equal(0, firstData.ErrorsRead);
        Assert.Equal(0, firstData.ErrorsWrite);
        Assert.Equal(0, firstData.ErrorsChecksum);

        var firstSpecial = layout.SpecialDevices[0];
        Assert.Equal("/dev/disk/by-id/nvme-WDC_PC_SN530_SDBPNPZ-256G-1006_205161805086-part1", firstSpecial.Path);
        Assert.Equal("special", firstSpecial.Role);
        Assert.Equal("ONLINE", firstSpecial.Status);
    }

    [Fact]
    public void ParseScrubInfo_ShouldParseFinishedScrub()
    {
        var json = File.ReadAllText("TestData/zpool_status.json");

        var scrub = ZpoolParser.ParseScrubInfo(json, "zfsPool");

        Assert.Equal("finished", scrub.State);
        Assert.Equal(0, scrub.Errors);
        Assert.Contains("12:54:52", scrub.StartTime);
        Assert.Contains("17:22:17", scrub.FinishTime);
    }

    [Fact]
    public void ParseScrubInfo_ShouldReturnIdleForMissingData()
    {
        var scrub = ZpoolParser.ParseScrubInfo("{}", "nonexistent");

        Assert.Equal("idle", scrub.State);
    }

    [Fact]
    public void ParseScrubInfo_ShouldParseScanningWithProgress()
    {
        var json = File.ReadAllText("TestData/zpool_status_scanning.json");

        var scrub = ZpoolParser.ParseScrubInfo(json, "zfsPool");

        Assert.Equal("running", scrub.State);
        Assert.Equal(0, scrub.Errors);
        Assert.Contains("09:07:04", scrub.StartTime);
        // issued=7.65T / to_examine=8.64T ≈ 88.54%
        Assert.True(scrub.ProgressPct > 88 && scrub.ProgressPct < 89,
            $"Expected ~88.54% but got {scrub.ProgressPct}%");
    }

    [Fact]
    public void ParsePoolLayout_ShouldDetectScrubOperation()
    {
        var json = File.ReadAllText("TestData/zpool_status_scanning.json");

        var layout = ZpoolParser.ParsePoolLayout(json, "zfsPool");

        Assert.Equal("scrubbing", layout.Operation);
    }

    [Fact]
    public void ParsePoolLayout_MiniTank_ShouldBeStripeWithTwoDisks()
    {
        var json = File.ReadAllText("TestData/zpool_status.json");

        var layout = ZpoolParser.ParsePoolLayout(json, "miniTank");

        Assert.Equal("stripe", layout.VdevType);
        Assert.Equal("", layout.Operation);
        Assert.Equal(2, layout.DataDevices.Count);
        Assert.Empty(layout.CacheDevices);
        Assert.Empty(layout.LogDevices);
        Assert.Empty(layout.SpareDevices);
        Assert.Empty(layout.SpecialDevices);
    }

    [Fact]
    public void ParseScrubInfo_MiniTank_ShouldBeIdle()
    {
        var json = File.ReadAllText("TestData/zpool_status.json");

        var scrub = ZpoolParser.ParseScrubInfo(json, "miniTank");

        Assert.Equal("idle", scrub.State);
    }

    [Fact]
    public void ParseScrubTimeLeft_ShouldExtractTimeToGo()
    {
        var text = """
              pool: zfsPool
             state: ONLINE
              scan: scrub in progress since Mon Mar 23 09:07:04 2026
                    8.64T / 8.64T scanned, 4.70T / 8.64T issued at 488M/s
                    0B repaired, 54.41% done, 02:20:57 to go
            """;

        var timeLeft = ZpoolParser.ParseScrubTimeLeft(text);

        Assert.Equal("02:20:57", timeLeft);
    }

    [Fact]
    public void ParseScrubTimeLeft_ShouldReturnEmptyWhenNoScrub()
    {
        var text = """
              pool: zfsPool
             state: ONLINE
              scan: scrub repaired 0B in 04:27:25 with 0 errors on Wed Mar 27 17:22:17 2024
            """;

        var timeLeft = ZpoolParser.ParseScrubTimeLeft(text);

        Assert.Equal("", timeLeft);
    }
}

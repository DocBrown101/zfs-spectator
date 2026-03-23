namespace Zfs.Tests;

using Zfs.Core.Services.Parser;

public class ZpoolParserTests
{
    [Fact]
    public void ParseSpecialVdev_ShouldSumCorrectly()
    {
        var json = File.ReadAllText("TestData/zpool_list_vdev.json");

        var (Size, Alloc, Free) = ZpoolParser.ParseSpecialVdevSize(json, "zfsPool");

        Assert.Equal(255550554112UL, Size);
        Assert.Equal(5491740672UL, Alloc);
        Assert.Equal(250058813440UL, Free);
    }

    [Fact]
    public void ParsePools_ShouldParsePoolProperties()
    {
        var json = File.ReadAllText("TestData/zpool_list.json");

        var pools = ZpoolParser.ParsePools(json);

        Assert.Single(pools);
        var pool = pools[0];
        Assert.Equal("zfsPool", pool.Name);
        Assert.Equal(24238647934976UL, pool.Size);
        Assert.Equal(13975181643776UL, pool.Alloc);
        Assert.Equal(10263466291200UL, pool.Free);
        Assert.Equal("ONLINE", pool.Health);
        Assert.Equal(3, pool.Fragmentation);
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

        Assert.Single(names);
        Assert.Equal("zfsPool", names[0]);
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

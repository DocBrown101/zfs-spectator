namespace Zfs.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using ZfsDashboard.Services;

public class DiskTemperatureTests
{
    private static readonly Dictionary<string, string> EmptyMap = [];
    private static readonly ILogger<DiskTemperatureBackgroundService> NullLogger = NullLogger<DiskTemperatureBackgroundService>.Instance;


    [Fact]
    public void ParseTemperatures_EmptyOutput_ReturnsEmpty()
    {
        Assert.Empty(DiskTemperatureBackgroundService.ParseTemperatures("", EmptyMap, NullLogger));
        Assert.Empty(DiskTemperatureBackgroundService.ParseTemperatures("  ", EmptyMap, NullLogger));
    }

    [Fact]
    public void ParseTemperatures_UnrecognizedDevice_SkipsLine()
    {
        var output = "dm-0\t0\t0\t0\t0\t0\t0\t40\n";

        var result = DiskTemperatureBackgroundService.ParseTemperatures(output, EmptyMap, NullLogger);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseTemperatures_PoolAndVdevLinesSkipped()
    {
        var output = string.Join("\n",
            "miniTank\t756G\t1.07T\t0\t2\t12.5K\t26.7K",
            "sda1\t407G\t521G\t0\t1\t6.39K\t13.1K\t23",
            "sdb1\t350G\t578G\t0\t1\t6.14K\t13.6K\t23",
            "");

        var result = DiskTemperatureBackgroundService.ParseTemperatures(output, EmptyMap, NullLogger);

        Assert.Equal(2, result.Count);
        Assert.Equal(23, result["sda"]);
        Assert.Equal(23, result["sdb"]);
    }

    [Fact]
    public void ParseTemperatures_ByIdNames_ResolvedViaDeviceMap()
    {
        var deviceMap = new Dictionary<string, string>
        {
            ["wwn-0x50014ee2b702ad1b"] = "sda",
            ["wwn-0x50014ee2b2d93b72"] = "sdb",
            ["ata-WDC_WD20EZRZ-00Z5HB0_WD-WCC4M5UDEPK1"] = "sdc",
        };

        var output = string.Join("\n",
            "miniTank\t756G\t1.07T\t0\t2\t12.5K\t26.7K",
            "wwn-0x50014ee2b702ad1b\t407G\t521G\t0\t1\t6.39K\t13.1K\t23",
            "wwn-0x50014ee2b2d93b72\t350G\t578G\t0\t1\t6.14K\t13.6K\t23",
            "zfsPool\t8.64T\t466G\t1\t3\t33.1K\t47.9K",
            "raidz1-0\t8.64T\t466G\t1\t3\t33.1K\t47.9K",
            "ata-WDC_WD20EZRZ-00Z5HB0_WD-WCC4M5UDEPK1\t-\t-\t0\t0\t6.39K\t9.67K\t22",
            "");

        var result = DiskTemperatureBackgroundService.ParseTemperatures(output, deviceMap, NullLogger);

        Assert.Equal(3, result.Count);
        Assert.Equal(23, result["sda"]);
        Assert.Equal(23, result["sdb"]);
        Assert.Equal(22, result["sdc"]);
    }

    [Fact]
    public void StripByIdPartition_WithPartSuffix_StripsIt()
    {
        Assert.Equal("wwn-0x50014ee2b702ad1b", DiskTemperatureBackgroundService.StripByIdPartition("wwn-0x50014ee2b702ad1b-part1"));
        Assert.Equal("ata-WDC_WD20EZRZ", DiskTemperatureBackgroundService.StripByIdPartition("ata-WDC_WD20EZRZ-part2"));
    }

    [Fact]
    public void StripByIdPartition_WithoutPartSuffix_ReturnsUnchanged()
    {
        Assert.Equal("wwn-0x50014ee2b702ad1b", DiskTemperatureBackgroundService.StripByIdPartition("wwn-0x50014ee2b702ad1b"));
        Assert.Equal("sda1", DiskTemperatureBackgroundService.StripByIdPartition("sda1"));
    }
}

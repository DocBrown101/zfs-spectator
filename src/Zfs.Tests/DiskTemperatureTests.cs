namespace Zfs.Tests;

using ZfsDashboard.Services;

public class DiskTemperatureTests
{
    [Fact]
    public void ParseTemperatures_StandardOutput_ExtractsValues()
    {
        var output = "tank\tsda1\t0\t0\t0\t0\t0\t0\t37\ntank\tsdb1\t0\t0\t0\t0\t0\t0\t42\n";

        var result = DiskTemperatureBackgroundService.ParseTemperatures(output);

        Assert.Equal(2, result.Count);
        Assert.Equal(37, result["sda"]);
        Assert.Equal(42, result["sdb"]);
    }

    [Fact]
    public void ParseTemperatures_NvmeDevices_ParsedCorrectly()
    {
        var output = "pool\tnvme0n1p1\t0\t0\t0\t0\t0\t0\t55\n";

        var result = DiskTemperatureBackgroundService.ParseTemperatures(output);

        Assert.Single(result);
        Assert.Equal(55, result["nvme0n1"]);
    }

    [Fact]
    public void ParseTemperatures_WholeDisks_ParsedCorrectly()
    {
        var output = "pool\tsda\t0\t0\t0\t0\t0\t0\t35\n";

        var result = DiskTemperatureBackgroundService.ParseTemperatures(output);

        Assert.Single(result);
        Assert.Equal(35, result["sda"]);
    }

    [Fact]
    public void ParseTemperatures_EmptyOutput_ReturnsEmpty()
    {
        Assert.Empty(DiskTemperatureBackgroundService.ParseTemperatures(""));
        Assert.Empty(DiskTemperatureBackgroundService.ParseTemperatures("  "));
    }

    [Fact]
    public void ParseTemperatures_NonNumericTemp_SkipsLine()
    {
        var output = "tank\tsda1\t0\t0\t0\t0\t0\t0\t-\ntank\tsdb1\t0\t0\t0\t0\t0\t0\t38\n";

        var result = DiskTemperatureBackgroundService.ParseTemperatures(output);

        Assert.Single(result);
        Assert.Equal(38, result["sdb"]);
    }

    [Fact]
    public void ParseTemperatures_TooFewFields_SkipsLine()
    {
        var output = "tank\tsda1\n";

        var result = DiskTemperatureBackgroundService.ParseTemperatures(output);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseTemperatures_DuplicateDevice_FirstOccurrenceWins()
    {
        var output = "pool1\tsda1\t0\t0\t0\t0\t0\t0\t35\npool2\tsda2\t0\t0\t0\t0\t0\t0\t40\n";

        var result = DiskTemperatureBackgroundService.ParseTemperatures(output);

        Assert.Single(result);
        Assert.Equal(35, result["sda"]);
    }

    [Fact]
    public void ParseTemperatures_UnrecognizedDevice_SkipsLine()
    {
        // dm-0 is not a physical disk
        var output = "pool\tdm-0\t0\t0\t0\t0\t0\t0\t40\n";

        var result = DiskTemperatureBackgroundService.ParseTemperatures(output);

        Assert.Empty(result);
    }
}

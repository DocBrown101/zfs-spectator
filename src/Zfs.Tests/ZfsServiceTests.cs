namespace Zfs.Tests;

using Zfs.Core.Models;
using Zfs.Core.Services;
using Zfs.Tests.Helper;

public class ZfsServiceTests
{
    [Theory]
    [InlineData("zfs-2.2.4-1\n", "2.2.4")]
    [InlineData("zfs-2.2.4\n", "2.2.4")]
    [InlineData("zfs-2.2.4-0ubuntu3.1\nzfs-kmod-2.2.4-1\n", "2.2.4")]
    [InlineData("zfs-2.1.15-1\n", "2.1.15")]
    public async Task GetZfsVersionAsync_ShouldExtractVersionFromOutput(string output, string expected)
    {
        var service = CreateService(new FakeCommandExecutor()
            .On("zfs", "version", output));

        Assert.Equal(expected, await service.GetZfsVersionAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("OpenZFS on Linux\n")]
    [InlineData("zfs-unknown\n")]
    public async Task GetZfsVersionAsync_NoVersionFound_ShouldReturnUnknown(string output)
    {
        var service = CreateService(new FakeCommandExecutor()
            .On("zfs", "version", output));

        Assert.Equal("unknown", await service.GetZfsVersionAsync());
    }

    [Fact]
    public async Task GetArcStatsAsync_ShouldParseProcValues()
    {
        var service = CreateService(new FakeCommandExecutor()
            .On("cat", "/proc/spl/kstat/zfs/arcstats", File.ReadAllText("TestData/arcstats.txt")));

        var arc = await service.GetArcStatsAsync();

        Assert.Equal(8589934592UL, arc.Size);
        Assert.Equal(17179869184UL, arc.MaxSize);
        Assert.Equal(9500000UL, arc.Hits);
        Assert.Equal(500000UL, arc.Misses);
        Assert.Equal(700000UL, arc.L2Hits);
        Assert.Equal(300000UL, arc.L2Misses);
        Assert.Equal(214748364800UL, arc.L2Size);
        Assert.Equal(4294967296UL, arc.MruSize);
        Assert.Equal(3221225472UL, arc.MfuSize);
        Assert.Equal(1073741824UL, arc.MetadataSize);
        Assert.Equal(7516192768UL, arc.DataSize);
    }

    [Fact]
    public async Task GetArcStatsAsync_UnparseableOutput_ShouldReturnZeros()
    {
        var service = CreateService(new FakeCommandExecutor()
            .On("cat", "/proc/spl/kstat/zfs/arcstats", "name type data\ngarbage\n"));

        var arc = await service.GetArcStatsAsync();

        Assert.Equal(new ArcStats(), arc);
    }

    private static ZfsService CreateService(FakeCommandExecutor executor) => new(executor, new ZpoolService(executor));
}

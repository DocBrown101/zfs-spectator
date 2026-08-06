namespace Zfs.Tests;

using Zfs.Core.Services;
using Zfs.Core.Services.TestData;

public class EmbeddedJsonCommandExecutorTests
{
    private readonly EmbeddedJsonCommandExecutor executor = new();

    public static TheoryData<string, string, string> KnownCommands => new()
    {
        { "zpool", "list -Hpvj -o name,size,alloc,free,health,frag", "zpool_list.json" },
        { "zpool", "status -Pj zfsPool", "zpool_status.json" },
        { "zpool", "status -Pj miniTank", "zpool_status.json" },
        { "zpool", "get -Hpj ashift zfsPool", "zpool_get_ashift.json" },
        { "zpool", "get -Hpj ashift miniTank", "zpool_get_ashift.json" },
        { "zfs", "get -Hpj used,available,compression zfsPool", "zfs_get_pool_props.json" },
        { "zfs", "get -Hpj used,available,compression miniTank", "zfs_get_pool_props.json" },
        { "zfs", "list -Hpj -t volume", "zfs_list_zvols.json" },
    };

    [Theory]
    [MemberData(nameof(KnownCommands))]
    public async Task KnownCommands_ReturnTheEmbeddedFixture(string command, string arguments, string expectedFile)
    {
        var output = await this.executor.ExecuteAsync(command, arguments);

        Assert.Equal(File.ReadAllText($"TestData/{expectedFile}"), output);
    }

    [Fact]
    public async Task ZfsVersion_ReturnsTheDemoVersion()
    {
        var output = await this.executor.ExecuteAsync("zfs", "version");

        Assert.Equal("2.3.1", output);
    }

    [Fact]
    public async Task ArcStats_ReturnsTheEmbeddedProcData()
    {
        var output = await this.executor.ExecuteAsync("cat", "/proc/spl/kstat/zfs/arcstats");

        Assert.Equal(File.ReadAllText("TestData/arcstats.txt"), output);
    }

    [Fact]
    public async Task UnknownCommand_ReturnsEmptyResponse()
    {
        var output = await this.executor.ExecuteAsync("zpool", "status --invalid");
        Assert.Equal("", output);

        output = await this.executor.ExecuteAsync("rm", "-rf /");
        Assert.Equal("", output);
    }

    [Theory]
    [InlineData("filesystem", "zfsPool", 9)]
    [InlineData("filesystem", "miniTank", 0)]
    [InlineData("snapshot", "zfsPool", 1)]
    [InlineData("snapshot", "miniTank", 0)]
    public async Task PoolScopedLists_OnlyReturnEntriesFromTheRequestedPool(
        string type,
        string poolName,
        int expectedCount)
    {
        var output = await this.executor.ExecuteAsync("zfs", $"list -Hpj -r -t {type} -o name {poolName}");
        using var document = System.Text.Json.JsonDocument.Parse(output);
        var datasets = document.RootElement.GetProperty("datasets").EnumerateObject().ToList();

        Assert.Equal(expectedCount, datasets.Count);
        Assert.All(datasets, item => Assert.Equal(poolName, item.Value.GetProperty("pool").GetString()));
    }

    [Fact]
    public async Task CancelledRequest_IsPropagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => this.executor.ExecuteAsync("zpool", "list", cts.Token));
    }

    [Fact]
    public async Task RealServices_ResolveAllDemoData()
    {
        var executor = new EmbeddedJsonCommandExecutor();
        var zpool = new ZpoolService(executor);
        var zfs = new ZfsService(executor, zpool);

        var poolNames = await zpool.GetPoolNamesAsync();
        Assert.Equal(["miniTank", "zfsPool"], poolNames.Order().ToList());

        var snapshots = await zpool.GetAllPoolsWithScrubAsync();
        Assert.Equal(2, snapshots.Count);
        Assert.Equal("finished", snapshots.Single(item => item.Pool.Name == "zfsPool").Scrub.State);
        Assert.Equal("idle", snapshots.Single(item => item.Pool.Name == "miniTank").Scrub.State);

        var datasets = await zfs.GetAllDatasetsAsync();
        Assert.Equal(9, datasets.Count);
        Assert.All(datasets, dataset => Assert.StartsWith("zfsPool", dataset.Name, StringComparison.Ordinal));

        Assert.Empty(await zfs.GetDatasetsAsync("miniTank"));

        Assert.NotEmpty(await zfs.GetSnapshotsAsync("zfsPool"));
        Assert.Empty(await zfs.GetSnapshotsAsync("miniTank"));
        Assert.NotEmpty(await zfs.GetAllZVolsAsync());

        var arc = await zfs.GetArcStatsAsync();
        Assert.Equal(8589934592UL, arc.Size);
        Assert.Equal(17179869184UL, arc.MaxSize);
        Assert.Equal(9500000UL, arc.Hits);

        Assert.Equal("2.3.1", await zfs.GetZfsVersionAsync());
    }
}

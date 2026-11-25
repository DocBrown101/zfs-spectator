using Zfs.Core.Models;
using Zfs.Core.Services;
using Zfs.Tests.Helper;
using ZfsDashboard.Pages.Snapshots;
using ZfsDashboard.ViewModels.Snapshots;

namespace Zfs.Tests;

public class SnapshotViewModelTests
{
    [Fact]
    public async Task Index_GroupsSnapshotsAcrossPoolsByNewestAndAssignsUniqueIdsAndCommands()
    {
        var executor = new FakeCommandExecutor().On("zpool", "list -Hpvj -o name,size,alloc,free,health,frag",
            File.ReadAllText("TestData/zpool_list.json"));
        var zfs = new TestDataHelpers.StubZfsService(pool => pool switch
        {
            "miniTank" =>
            [
                Snapshot("miniTank/data@old", "miniTank/data", "old", "2026-01-01T10:00:00Z"),
                Snapshot("miniTank/data@daily", "miniTank/data", "daily", "2026-02-01T10:00:00Z"),
            ],
            "zfsPool" => [Snapshot("zfsPool/data@daily", "zfsPool/data", "daily", "2026-03-01T10:00:00Z")],
            _ => throw new ArgumentException("Unexpected pool", nameof(pool)),
        });
        var page = new IndexModel(zfs, new ZpoolService(executor));

        await page.OnGetAsync();

        Assert.Equal(3, page.SnapshotCount);
        Assert.Equal(["zfsPool/data", "miniTank/data"], page.SnapshotGroups.Select(group => group.DatasetName));
        Assert.All(page.SnapshotGroups, group => Assert.True(group.EnableContextMenu));
        Assert.Equal(["daily", "old"], page.SnapshotGroups[1].Rows.Select(row => row.Name));
        var rows = page.SnapshotGroups.SelectMany(group => group.Rows).ToList();
        Assert.Equal(["snapshot-group-0-0", "snapshot-group-1-0", "snapshot-group-1-1"], rows.Select(row => row.CollapseId));
        Assert.Equal("sudo zfs rollback -r 'zfsPool/data@daily'", rows[0].RollbackCommand.Command);
        Assert.Equal("sudo zfs destroy 'miniTank/data@daily'", rows[1].DestroyCommand.Command);
    }

    [Fact]
    public void Group_UsesMinutePrecisionAndFormatsSizes()
    {
        var snapshot = Snapshot("tank/data@daily", "tank/data", "daily", "2026-01-01T10:11:12Z") with
        {
            Used = 1024,
            Refer = 1024 * 1024,
        };

        var row = Assert.Single(new SnapshotGroupViewModel("tank/data", [snapshot]).Rows);

        Assert.Equal(snapshot.Creation.LocalDateTime.ToString("yyyy-MM-dd HH:mm"), row.Created);
        Assert.Equal("1 KiB", row.Used);
        Assert.Equal("1 MiB", row.Referenced);
    }

    [Fact]
    public void Group_IncludesSecondsWhenRequested()
    {
        var snapshot = Snapshot("tank/data@daily", "tank/data", "daily", "2026-01-01T10:11:12Z");

        var row = Assert.Single(new SnapshotGroupViewModel("tank/data", [snapshot], includeSeconds: true).Rows);

        Assert.Equal(snapshot.Creation.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"), row.Created);
    }

    [Fact]
    public void Group_ExposesStableAccordionId()
    {
        var group = new SnapshotGroupViewModel("tank/data", [], idPrefix: "snapshot-group-0");

        Assert.Equal("snapshot-group-0-accordion", group.AccordionId);
    }

    [Fact]
    public void Group_ContextMenu_IsOptIn()
    {
        var defaultGroup = new SnapshotGroupViewModel("tank/data", []);
        var contextGroup = new SnapshotGroupViewModel("tank/data", [], enableContextMenu: true);

        Assert.False(defaultGroup.EnableContextMenu);
        Assert.True(contextGroup.EnableContextMenu);
    }

    [Fact]
    public void Group_EmptySnapshots_ExposesEmptyMessage()
    {
        var group = new SnapshotGroupViewModel("tank/data", []);

        Assert.Empty(group.Rows);
        Assert.Equal("No snapshots", group.EmptyMessage);
    }

    private static Snapshot Snapshot(string name, string dataset, string snapshotName, string created) => new()
    {
        Name = name,
        DatasetName = dataset,
        SnapName = snapshotName,
        Creation = DateTimeOffset.Parse(created),
    };
}

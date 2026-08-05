using Zfs.Core.Models;
using ZfsDashboard.ViewModels.Snapshots;

namespace Zfs.Tests;

public class SnapshotViewModelTests
{
    [Fact]
    public void PageViewModel_SortsGroupsAndRowsByNewestSnapshot()
    {
        var snapshots = new[]
        {
            Snapshot("tank/data@old", "tank/data", "old", "2026-01-01T10:00:00Z"),
            Snapshot("backup/data@newest", "backup/data", "newest", "2026-03-01T10:00:00Z"),
            Snapshot("tank/data@new", "tank/data", "new", "2026-02-01T10:00:00Z"),
        };

        var groups = new SnapshotPageViewModel(snapshots).Groups;

        Assert.Equal(["backup/data", "tank/data"], groups.Select(group => group.DatasetName));
        Assert.Equal(["new", "old"], groups[1].Table.Rows.Select(row => row.Name));
    }

    [Fact]
    public void PageViewModel_CreatesUniqueIdsAndCommands()
    {
        var snapshots = new[]
        {
            Snapshot("tank/a@daily", "tank/a", "daily", "2026-01-01T10:00:00Z"),
            Snapshot("tank/b@daily", "tank/b", "daily", "2026-01-01T11:00:00Z"),
        };

        var rows = new SnapshotPageViewModel(snapshots).Groups
            .SelectMany(group => group.Table.Rows)
            .ToList();

        Assert.Equal(rows.Count, rows.Select(row => row.CollapseId).Distinct().Count());
        Assert.Contains("sudo zfs rollback -r tank/a@daily", rows.Select(row => row.RollbackCommand.Command));
        Assert.Contains("sudo zfs destroy tank/b@daily", rows.Select(row => row.DestroyCommand.Command));
    }

    [Fact]
    public void TableViewModel_UsesMinutePrecisionAndFormatsSizes()
    {
        var snapshot = Snapshot("tank/data@daily", "tank/data", "daily", "2026-01-01T10:11:12Z") with
        {
            Used = 1024,
            Refer = 1024 * 1024,
        };

        var row = Assert.Single(new SnapshotTableViewModel([snapshot]).Rows);

        Assert.Equal(snapshot.Creation.LocalDateTime.ToString("yyyy-MM-dd HH:mm"), row.Created);
        Assert.Equal("1 KiB", row.Used);
        Assert.Equal("1 MiB", row.Referenced);
    }

    private static Snapshot Snapshot(string name, string dataset, string snapshotName, string created) => new()
    {
        Name = name,
        DatasetName = dataset,
        SnapName = snapshotName,
        Creation = DateTimeOffset.Parse(created),
    };
}

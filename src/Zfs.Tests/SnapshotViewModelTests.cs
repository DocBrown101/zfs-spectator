using Zfs.Core.Models;
using ZfsDashboard.ViewModels.Snapshots;

namespace Zfs.Tests;

public class SnapshotViewModelTests
{
    [Fact]
    public void Groups_AreOrderedByNewestSnapshotAndGroupedByDataset()
    {
        var snapshots = new[]
        {
            Snapshot("tank/data@old", "tank/data", "old", "2026-01-01T10:00:00Z"),
            Snapshot("backup/data@newest", "backup/data", "newest", "2026-03-01T10:00:00Z"),
            Snapshot("tank/data@new", "tank/data", "new", "2026-02-01T10:00:00Z"),
        };

        var groups = Group(snapshots);

        Assert.Equal(["backup/data", "tank/data"], groups.Select(group => group.DatasetName));
        Assert.Equal(["new", "old"], groups[1].Rows.Select(row => row.Name));
    }

    [Fact]
    public void Groups_CreateUniqueIdsAndCommands()
    {
        var snapshots = new[]
        {
            Snapshot("tank/a@daily", "tank/a", "daily", "2026-01-01T10:00:00Z"),
            Snapshot("tank/b@daily", "tank/b", "daily", "2026-01-01T11:00:00Z"),
        };

        var rows = Group(snapshots).SelectMany(group => group.Rows).ToList();

        Assert.Equal(rows.Count, rows.Select(row => row.CollapseId).Distinct().Count());
        Assert.Equal(["snapshot-group-0-0", "snapshot-group-1-0"], rows.Select(row => row.CollapseId));
        Assert.Contains("sudo zfs rollback -r tank/a@daily", rows.Select(row => row.RollbackCommand.Command));
        Assert.Contains("sudo zfs destroy tank/b@daily", rows.Select(row => row.DestroyCommand.Command));
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
    public void Group_EmptySnapshots_ExposesEmptyMessage()
    {
        var group = new SnapshotGroupViewModel("tank/data", []);

        Assert.Empty(group.Rows);
        Assert.Equal("No snapshots", group.EmptyMessage);
    }

    private static IReadOnlyList<SnapshotGroupViewModel> Group(IEnumerable<Snapshot> snapshots) =>
        snapshots
            .OrderByDescending(snapshot => snapshot.Creation)
            .GroupBy(snapshot => snapshot.DatasetName)
            .Select((group, index) => new SnapshotGroupViewModel(
                group.Key,
                group,
                idPrefix: $"snapshot-group-{index}",
                includeSeconds: true))
            .ToList();

    private static Snapshot Snapshot(string name, string dataset, string snapshotName, string created) => new()
    {
        Name = name,
        DatasetName = dataset,
        SnapName = snapshotName,
        Creation = DateTimeOffset.Parse(created),
    };
}

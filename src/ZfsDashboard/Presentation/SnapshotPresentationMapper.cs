using Zfs.Core.Models;
using Zfs.Core.Services;
using ZfsDashboard.Models;

namespace ZfsDashboard.Presentation;

public static class SnapshotPresentationMapper
{
    public static IReadOnlyList<SnapshotGroupViewModel> MapGroups(IReadOnlyList<Snapshot> snapshots)
    {
        return snapshots
            .OrderByDescending(snapshot => snapshot.Creation)
            .GroupBy(snapshot => snapshot.DatasetName)
            .Select((group, index) => new SnapshotGroupViewModel
            {
                DatasetName = group.Key,
                Table = MapTable(group, $"snapshot-group-{index}", includeSeconds: true),
            })
            .ToList();
    }

    public static SnapshotTableViewModel MapDatasetTable(IReadOnlyList<Snapshot> snapshots)
        => MapTable(snapshots, "dataset-snapshot", includeSeconds: false);

    private static SnapshotTableViewModel MapTable(
        IEnumerable<Snapshot> snapshots,
        string idPrefix,
        bool includeSeconds)
    {
        var rows = snapshots
            .OrderByDescending(snapshot => snapshot.Creation)
            .Select((snapshot, index) => new SnapshotRowViewModel
            {
                CollapseId = $"{idPrefix}-{index}",
                Name = snapshot.SnapName,
                Used = snapshot.Used.FormatBytes(),
                Referenced = snapshot.Refer.FormatBytes(),
                Created = snapshot.Creation.LocalDateTime.ToString(includeSeconds ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm"),
                RollbackCommand = new CopyableCommandViewModel(
                    CommandSuggestionsService.SuggestRollback(snapshot.Name).ZfsCommand,
                    Compact: true),
                DestroyCommand = new CopyableCommandViewModel(
                    CommandSuggestionsService.SuggestDestroySnapshot(snapshot.Name).ZfsCommand,
                    Compact: true),
            })
            .ToList();

        return new SnapshotTableViewModel { Rows = rows };
    }
}

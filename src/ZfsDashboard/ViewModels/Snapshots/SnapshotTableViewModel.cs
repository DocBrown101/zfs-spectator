using Zfs.Core.Models;

namespace ZfsDashboard.ViewModels.Snapshots;

public sealed record SnapshotTableViewModel
{
    public SnapshotTableViewModel(
        IEnumerable<Snapshot> snapshots,
        string idPrefix = "dataset-snapshot",
        bool includeSeconds = false)
    {
        this.Rows = snapshots
            .OrderByDescending(snapshot => snapshot.Creation)
            .Select((snapshot, index) => new SnapshotRowViewModel(
                snapshot,
                $"{idPrefix}-{index}",
                includeSeconds))
            .ToList();
    }

    public IReadOnlyList<SnapshotRowViewModel> Rows { get; }
    public string EmptyMessage { get; } = "No snapshots";
}

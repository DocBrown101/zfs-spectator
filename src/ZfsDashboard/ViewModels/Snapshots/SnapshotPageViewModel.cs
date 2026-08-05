using Zfs.Core.Models;

namespace ZfsDashboard.ViewModels.Snapshots;

public sealed record SnapshotPageViewModel
{
    public SnapshotPageViewModel(IReadOnlyList<Snapshot> snapshots)
    {
        this.TotalCount = snapshots.Count;
        this.Groups = snapshots
            .OrderByDescending(snapshot => snapshot.Creation)
            .GroupBy(snapshot => snapshot.DatasetName)
            .Select((group, index) => new SnapshotGroupViewModel(group.Key, group, index))
            .ToList();
    }

    public int TotalCount { get; }
    public IReadOnlyList<SnapshotGroupViewModel> Groups { get; }
}

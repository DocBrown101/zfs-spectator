using Zfs.Core.Models;

namespace ZfsDashboard.ViewModels.Snapshots;

public sealed record SnapshotGroupViewModel
{
    public SnapshotGroupViewModel(string datasetName, IEnumerable<Snapshot> snapshots, int index)
    {
        this.DatasetName = datasetName;
        this.Table = new SnapshotTableViewModel(snapshots, $"snapshot-group-{index}", includeSeconds: true);
    }

    public string DatasetName { get; }
    public SnapshotTableViewModel Table { get; }
}

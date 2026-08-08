using Zfs.Core.Models;

namespace ZfsDashboard.ViewModels.Snapshots;

public sealed record SnapshotGroupViewModel
{
    public SnapshotGroupViewModel(
        string datasetName,
        IEnumerable<Snapshot> snapshots,
        string idPrefix = "dataset-snapshot",
        bool includeSeconds = false)
    {
        this.DatasetName = datasetName;
        this.AccordionId = $"{idPrefix}-accordion";
        this.Rows = snapshots
            .OrderByDescending(snapshot => snapshot.Creation)
            .Select((snapshot, index) => new SnapshotRowViewModel(
                snapshot,
                $"{idPrefix}-{index}",
                includeSeconds))
            .ToList();
    }

    public string DatasetName { get; }
    public string AccordionId { get; }
    public IReadOnlyList<SnapshotRowViewModel> Rows { get; }
    public string EmptyMessage { get; } = "No snapshots";
}

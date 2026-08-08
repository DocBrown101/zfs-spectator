using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;
using ZfsDashboard.ViewModels.Snapshots;

namespace ZfsDashboard.Pages.Snapshots;

public class IndexModel(IZfsService zfs, IZpoolService zpool) : PageModel
{
    public IReadOnlyList<SnapshotGroupViewModel> SnapshotGroups { get; private set; } = [];
    public int SnapshotCount { get; private set; }
    public List<CommandSuggestion> Suggestions { get; } = [];

    public async Task OnGetAsync()
    {
        var snapshots = new List<Snapshot>();
        var poolNames = new List<string>();
        foreach (var (pool, snaps) in await ZfsAggregation.GetAllByPoolAsync(zpool, zfs.GetSnapshotsAsync))
        {
            poolNames.Add(pool);
            snapshots.AddRange(snaps);
        }

        this.SnapshotCount = snapshots.Count;
        this.SnapshotGroups = snapshots
            .OrderByDescending(snapshot => snapshot.Creation)
            .GroupBy(snapshot => snapshot.DatasetName)
            .Select((group, index) => new SnapshotGroupViewModel(
                group.Key,
                group,
                idPrefix: $"snapshot-group-{index}",
                includeSeconds: true))
            .ToList();

        foreach (var pool in poolNames.Order())
            this.Suggestions.Add(CommandSuggestionsService.SuggestCreateSnapshot(pool));
    }
}

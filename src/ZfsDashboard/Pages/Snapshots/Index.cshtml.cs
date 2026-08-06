using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;
using ZfsDashboard.ViewModels.Snapshots;

namespace ZfsDashboard.Pages.Snapshots;

public class IndexModel(IZfsService zfs, IZpoolService zpool) : PageModel
{
    public SnapshotPageViewModel Snapshots { get; private set; } = new([]);
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

        this.Snapshots = new SnapshotPageViewModel(snapshots);

        foreach (var pool in poolNames.Order())
            this.Suggestions.Add(CommandSuggestionsService.SuggestCreateSnapshot(pool));
    }
}

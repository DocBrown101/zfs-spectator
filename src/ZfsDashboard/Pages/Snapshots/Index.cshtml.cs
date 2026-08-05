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
        var names = await zpool.GetPoolNamesAsync();
        var snapshots = new List<Snapshot>();
        var tasks = names.Select(n => zfs.GetSnapshotsAsync(n));
        foreach (var snaps in await Task.WhenAll(tasks))
            snapshots.AddRange(snaps);

        this.Snapshots = new SnapshotPageViewModel(snapshots);

        foreach (var pool in names.Order())
            this.Suggestions.Add(CommandSuggestionsService.SuggestCreateSnapshot(pool));
    }
}

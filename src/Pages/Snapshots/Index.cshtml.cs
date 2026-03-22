using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;

namespace ZfsDashboard.Pages.Snapshots;

public class IndexModel(ZfsService zfs, ZpoolService zpool) : PageModel
{
    public List<Snapshot> Snapshots { get; private set; } = [];
    public List<CommandSuggestion> Suggestions { get; } = [];

    public async Task OnGetAsync()
    {
        var names = await zpool.GetPoolNamesAsync();
        var tasks = names.Select(n => zfs.GetSnapshotsAsync(n));
        foreach (var snaps in await Task.WhenAll(tasks))
            this.Snapshots.AddRange(snaps);

        foreach (var pool in names.Order())
            this.Suggestions.Add(CommandSuggestionsService.SuggestCreateSnapshot(pool));
    }
}

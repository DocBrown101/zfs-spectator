using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;
using ZfsDashboard.Models;
using ZfsDashboard.Presentation;

namespace ZfsDashboard.Pages.Snapshots;

public class IndexModel(IZfsService zfs, IZpoolService zpool) : PageModel
{
    public IReadOnlyList<SnapshotGroupViewModel> Groups { get; private set; } = [];
    public int TotalCount { get; private set; }
    public List<CommandSuggestion> Suggestions { get; } = [];

    public async Task OnGetAsync()
    {
        var names = await zpool.GetPoolNamesAsync();
        var snapshots = new List<Snapshot>();
        var tasks = names.Select(n => zfs.GetSnapshotsAsync(n));
        foreach (var snaps in await Task.WhenAll(tasks))
            snapshots.AddRange(snaps);

        this.TotalCount = snapshots.Count;
        this.Groups = SnapshotPresentationMapper.MapGroups(snapshots);

        foreach (var pool in names.Order())
            this.Suggestions.Add(CommandSuggestionsService.SuggestCreateSnapshot(pool));
    }
}

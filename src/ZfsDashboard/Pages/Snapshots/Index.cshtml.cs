using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;
using ZfsDashboard.ViewModels.Snapshots;
using ZfsDashboard.ViewModels.Shared;

namespace ZfsDashboard.Pages.Snapshots;

public class IndexModel(IZfsService zfs, IZpoolService zpool) : PageModel
{
    public IReadOnlyList<SnapshotGroupViewModel> SnapshotGroups { get; private set; } = [];
    public int SnapshotCount { get; private set; }
    public CommandMenuViewModel ContextCommands { get; } = new(
        "snapshot-command-menu",
        [
            new(CommandSuggestionsService.SuggestCreateSnapshot(CommandMenuViewModel.DatasetToken), "bi-camera"),
            new(CommandSuggestionsService.SuggestRollback(CommandMenuViewModel.SnapshotToken), "bi-arrow-counterclockwise", IsDangerous: true),
            new(CommandSuggestionsService.SuggestDestroySnapshot(CommandMenuViewModel.SnapshotToken), "bi-trash3", IsDangerous: true),
        ]);

    public async Task OnGetAsync()
    {
        var snapshots = new List<Snapshot>();
        foreach (var (_, snaps) in await ZfsAggregation.GetAllByPoolAsync(zpool, zfs.GetSnapshotsAsync))
        {
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
                includeSeconds: true,
                enableContextMenu: true))
            .ToList();
    }
}

using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;
using ZfsDashboard.ViewModels.Shared;

namespace ZfsDashboard.Pages.Datasets;

public class IndexModel(IZfsService zfs, IZpoolService zpool) : PageModel
{
    public Dictionary<string, List<Dataset>> DatasetsByPool { get; private set; } = [];
    public int TotalCount { get; private set; }
    public CommandMenuViewModel ContextCommands { get; } = new(
        "dataset-command-menu",
        [
            new(CommandSuggestionsService.SuggestCreateChildDataset(CommandMenuViewModel.DatasetToken), "bi-folder-plus"),
            new(CommandSuggestionsService.SuggestCreateSnapshot(CommandMenuViewModel.DatasetToken), "bi-camera"),
            new(CommandSuggestionsService.SuggestRenameDataset(CommandMenuViewModel.DatasetToken), "bi-pencil"),
            new(CommandSuggestionsService.SuggestDestroyDatasetRecursive(CommandMenuViewModel.DatasetToken), "bi-trash3", IsDangerous: true),
        ]);

    public async Task OnGetAsync()
    {
        foreach (var (pool, datasets) in await ZfsAggregation.GetAllByPoolAsync(zpool, zfs.GetDatasetsAsync))
        {
            this.DatasetsByPool[pool] = datasets;
            this.TotalCount += datasets.Count;
        }
    }
}

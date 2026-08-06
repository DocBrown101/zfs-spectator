using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;

namespace ZfsDashboard.Pages.Datasets;

public class IndexModel(IZfsService zfs, IZpoolService zpool) : PageModel
{
    public Dictionary<string, List<Dataset>> DatasetsByPool { get; private set; } = [];
    public List<CommandSuggestion> Suggestions { get; } = [];
    public int TotalCount { get; private set; }

    public async Task OnGetAsync()
    {
        foreach (var (pool, datasets) in await ZfsAggregation.GetAllByPoolAsync(zpool, zfs.GetDatasetsAsync))
        {
            this.DatasetsByPool[pool] = datasets;
            this.TotalCount += datasets.Count;
        }
        foreach (var pool in this.DatasetsByPool.Keys.Order())
        {
            this.Suggestions.Add(CommandSuggestionsService.SuggestCreateChildDataset(pool));
            this.Suggestions.Add(CommandSuggestionsService.SuggestDeleteChildDataset(pool));
        }
    }
}

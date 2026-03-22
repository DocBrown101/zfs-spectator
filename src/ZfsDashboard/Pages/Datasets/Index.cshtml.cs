using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;

namespace ZfsDashboard.Pages.Datasets;

public class IndexModel(ZfsService zfs, ZpoolService zpool) : PageModel
{
    public Dictionary<string, List<Dataset>> DatasetsByPool { get; private set; } = [];
    public List<Dataset> MountedDatasets { get; private set; } = [];
    public List<CommandSuggestion> Suggestions { get; } = [];
    public int TotalCount { get; private set; }

    public async Task OnGetAsync()
    {
        var names = await zpool.GetPoolNamesAsync();
        var tasks = names.Select(async n => (Pool: n, Datasets: await zfs.GetDatasetsAsync(n)));
        foreach (var (pool, datasets) in await Task.WhenAll(tasks))
        {
            this.DatasetsByPool[pool] = datasets;
            this.TotalCount += datasets.Count;
        }
        this.MountedDatasets = this.DatasetsByPool.Values
            .SelectMany(d => d)
            .Where(d => d.Mounted)
            .OrderBy(d => d.Mountpoint)
            .ToList();

        foreach (var pool in this.DatasetsByPool.Keys.Order())
            this.Suggestions.Add(CommandSuggestionsService.SuggestCreateChildDataset(pool));
    }
}

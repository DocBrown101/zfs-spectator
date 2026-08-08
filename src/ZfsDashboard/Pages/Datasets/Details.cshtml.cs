using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;
using ZfsDashboard.ViewModels.Snapshots;

namespace ZfsDashboard.Pages.Datasets;

public class DetailsModel(IZfsService zfs) : PageModel
{
    public Dataset? Dataset { get; private set; }
    public SnapshotGroupViewModel SnapshotGroup { get; private set; } = new("", []);
    public List<CommandSuggestion> Suggestions { get; } = [];

    public async Task<IActionResult> OnGetAsync([FromQuery] string name)
    {
        if (string.IsNullOrEmpty(name)) return this.NotFound();

        var allDs = await zfs.GetAllDatasetsAsync();
        this.Dataset = allDs.FirstOrDefault(d => d.Name == name);
        if (this.Dataset is null) return this.NotFound();

        var poolName = name.Split('/').First();
        var allSnaps = await zfs.GetSnapshotsAsync(poolName);
        var snapshots = allSnaps.Where(s => s.DatasetName == name).ToList();
        this.SnapshotGroup = new SnapshotGroupViewModel(name, snapshots);

        this.Suggestions.Add(CommandSuggestionsService.SuggestCreateChildDataset(name));
        this.Suggestions.Add(CommandSuggestionsService.SuggestCreateSnapshot(name));
        this.Suggestions.Add(CommandSuggestionsService.SuggestSetProperty(name, "recordsize", "1M"));
        this.Suggestions.Add(CommandSuggestionsService.SuggestSetProperty(name, "special_small_blocks", "128K"));
        if (this.Dataset.Compression == "off")
            this.Suggestions.Add(CommandSuggestionsService.SuggestSetProperty(name, "compression", "lz4"));
        if (this.Dataset.Quota == 0)
            this.Suggestions.Add(CommandSuggestionsService.SuggestSetProperty(name, "quota", "100G"));
        this.Suggestions.Add(CommandSuggestionsService.SuggestRenameDataset(name));
        this.Suggestions.Add(CommandSuggestionsService.SuggestDestroyDataset(name));

        return this.Page();
    }
}

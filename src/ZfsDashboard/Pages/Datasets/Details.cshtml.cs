using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;
using ZfsDashboard.ViewModels.Shared;
using ZfsDashboard.ViewModels.Snapshots;

namespace ZfsDashboard.Pages.Datasets;

public class DetailsModel(IZfsService zfs, IZpoolService zpool) : PageModel
{
    public Dataset? Dataset { get; private set; }
    public EncryptionDetailsViewModel? Encryption { get; private set; }
    public SnapshotGroupViewModel SnapshotGroup { get; private set; } = new("", []);
    public List<CommandSuggestion> Suggestions { get; } = [];

    public async Task<IActionResult> OnGetAsync([FromQuery] string name)
    {
        if (string.IsNullOrEmpty(name)) return this.NotFound();

        var poolName = name.Split('/', 2)[0];
        if (!(await zpool.GetPoolNamesAsync()).Contains(poolName)) return this.NotFound();

        var datasets = await zfs.GetDatasetsAsync(poolName);
        this.Dataset = datasets.FirstOrDefault(d => d.Name == name);
        if (this.Dataset is null) return this.NotFound();
        if (this.Dataset.Encrypted)
        {
            var root = datasets.FirstOrDefault(d => d.Name == this.Dataset.EncryptionRoot);
            this.Encryption = new("Encryption", this.Dataset.EncryptionAlgorithm, this.Dataset.KeyLocked,
                root?.KeySettings ?? this.Dataset.KeySettings, root?.Name);
            if (root is not null)
                this.Suggestions.Add(CommandSuggestionsService.SuggestSetProperty(root.Name, "keylocation", "file:///etc/zfs/new.key"));
        }

        var allSnaps = await zfs.GetSnapshotsAsync(poolName);
        var snapshots = allSnaps.Where(s => s.DatasetName == name).ToList();
        this.SnapshotGroup = new SnapshotGroupViewModel(name, snapshots);

        this.Suggestions.Add(CommandSuggestionsService.SuggestCreateChildDataset(name));
        this.Suggestions.Add(CommandSuggestionsService.SuggestCreateSnapshot(name));
        this.Suggestions.Add(CommandSuggestionsService.SuggestSetProperty(name, "compression", this.Dataset.Compression == "off" ? "lz4" : "off"));
        this.Suggestions.Add(CommandSuggestionsService.SuggestSetProperty(name, "quota", this.Dataset.Quota == 0 ? "100G" : "none"));
        this.Suggestions.Add(CommandSuggestionsService.SuggestSetProperty(name, "recordsize", "1M"));
        this.Suggestions.Add(CommandSuggestionsService.SuggestSetProperty(name, "special_small_blocks", "128K"));
        this.Suggestions.Add(CommandSuggestionsService.SuggestRenameDataset(name));
        this.Suggestions.Add(CommandSuggestionsService.SuggestDestroyDataset(name));

        return this.Page();
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;

namespace ZfsDashboard.Pages.Pools;

public class DetailsModel(ZpoolService zpool) : PageModel
{
    public Pool? Pool { get; private set; }
    public ScrubInfo Scrub { get; private set; } = new() { State = "idle" };
    public List<CommandSuggestion> Suggestions { get; } = [];

    public async Task<IActionResult> OnGetAsync(string name)
    {
        this.Pool = await zpool.GetPoolByNameAsync(name);
        if (this.Pool is null) return this.NotFound();

        this.Scrub = await zpool.GetScrubStatusAsync(name);

        this.Suggestions.Add(CommandSuggestionsService.SuggestCreateChildDataset(name));
        this.Suggestions.Add(CommandSuggestionsService.SuggestScrub(name));
        this.Suggestions.Add(CommandSuggestionsService.SuggestSetSpecialSmallBlocks(name));
        this.Suggestions.Add(CommandSuggestionsService.SuggestPoolExport(name));
        if (this.Pool.Compression == "off")
            this.Suggestions.Add(CommandSuggestionsService.SuggestSetProperty(name, "compression", "lz4"));
        if (this.Pool.Atime == "on")
            this.Suggestions.Add(CommandSuggestionsService.SuggestSetProperty(name, "atime", "off"));

        return this.Page();
    }
}

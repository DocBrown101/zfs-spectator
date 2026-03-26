using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;

namespace ZfsDashboard.Pages.Pools;

public class DetailsModel(IZpoolService zpool) : PageModel
{
    public Pool? Pool { get; private set; }
    public ScrubInfo Scrub { get; private set; } = ScrubInfo.Idle;
    public List<CommandSuggestion> Suggestions { get; } = [];

    public async Task<IActionResult> OnGetAsync(string name)
    {
        var result = await zpool.GetPoolWithScrubAsync(name);
        if (result is null) return this.NotFound();

        (this.Pool, this.Scrub) = result.Value;

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

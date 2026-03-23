using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;

namespace ZfsDashboard.Pages.Pools;

public class IndexModel(IZpoolService zpool) : PageModel
{
    public List<Pool> Pools { get; private set; } = [];
    public List<CommandSuggestion> Suggestions { get; } = [];

    public async Task OnGetAsync()
    {
        this.Pools = await zpool.GetAllPoolsAsync();

        this.Suggestions.Add(CommandSuggestionsService.SuggestCreatePool());
        this.Suggestions.Add(CommandSuggestionsService.SuggestPoolImport("new_external_pool"));
    }
}

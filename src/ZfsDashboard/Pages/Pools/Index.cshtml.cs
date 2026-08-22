using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;
using ZfsDashboard.ViewModels.Shared;

namespace ZfsDashboard.Pages.Pools;

public class IndexModel(IZpoolService zpool) : PageModel
{
    public List<Pool> Pools { get; private set; } = [];
    public CommandMenuViewModel ContextCommands { get; } = new(
        "pool-command-menu",
        [
            new(CommandSuggestionsService.SuggestScrub(CommandMenuViewModel.PoolToken), "bi-arrow-repeat"),
            new(CommandSuggestionsService.SuggestPoolExport(CommandMenuViewModel.PoolToken), "bi-box-arrow-right"),
        ]);
    public CommandMenuViewModel GlobalCommands { get; } = new(
        "pool-global-command-menu",
        [
            new(CommandSuggestionsService.SuggestCreatePool(), "bi-plus-circle"),
            new(CommandSuggestionsService.SuggestPoolImport("new_external_pool"), "bi-box-arrow-in-down"),
        ],
        TriggerLabel: "Global commands");

    public async Task OnGetAsync()
    {
        this.Pools = await zpool.GetAllPoolsAsync();
    }
}

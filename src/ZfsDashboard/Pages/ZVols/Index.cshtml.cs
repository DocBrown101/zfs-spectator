using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;

namespace ZfsDashboard.Pages.ZVols;

public class IndexModel(IZfsService zfs) : PageModel
{
    public List<ZVol> ZVols { get; private set; } = [];

    public CommandSuggestion CommandSuggestion { get; private set; } = CommandSuggestionsService.SuggestCreateZVol("poolname");

    public async Task OnGetAsync()
    {
        this.ZVols = await zfs.GetAllZVolsAsync();
    }
}

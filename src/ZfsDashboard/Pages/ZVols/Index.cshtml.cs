using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;

namespace ZfsDashboard.Pages.ZVols;

public class IndexModel(IZfsService zfs) : PageModel
{
    public List<ZVol> ZVols { get; private set; } = [];

    public async Task OnGetAsync()
    {
        this.ZVols = await zfs.GetAllZVolsAsync();
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ZfsDashboard.Services;
using ZfsDashboard.ViewModels.Dashboard;

namespace ZfsDashboard.Pages;

public class IndexModel(IDashboardSnapshotProvider snapshots) : PageModel
{
    public DashboardPageViewModel Dashboard { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        var snapshot = await snapshots.GetSnapshotAsync(this.HttpContext.RequestAborted);
        this.Dashboard = new DashboardPageViewModel(
            snapshot.Pools,
            snapshot.Data.System,
            snapshot.StaticSystem);
    }

    public IActionResult OnGetLive()
    {
        this.Response.Headers.CacheControl = "no-store";
        if (snapshots.Current is not { } snapshot)
        {
            this.Response.Headers.RetryAfter = "1";
            return this.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return new JsonResult(new DashboardLiveViewModel(snapshot));
    }
}

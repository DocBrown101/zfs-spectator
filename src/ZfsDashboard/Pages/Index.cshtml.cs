using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ZfsDashboard.Services;
using ZfsDashboard.ViewModels.Dashboard;

namespace ZfsDashboard.Pages;

public class IndexModel(IDashboardSnapshotProvider snapshots) : PageModel
{
    public DashboardLiveViewModel Live { get; private set; } = null!;
    public CpuCardViewModel Cpu { get; private set; } = null!;
    public IReadOnlyList<PoolCardViewModel> Pools { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        var snapshot = await snapshots.GetSnapshotAsync(this.HttpContext.RequestAborted);
        this.Live = new DashboardLiveViewModel(snapshot);
        this.Cpu = new CpuCardViewModel(snapshot.Data.System, snapshot.StaticSystem);
        this.Pools = snapshot.Pools.Select(pool => new PoolCardViewModel(pool)).ToList();
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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;
using ZfsDashboard.Models;
using ZfsDashboard.Presentation;

namespace ZfsDashboard.Pages;

public class IndexModel(IZfsService zfs, IZpoolService zpool, ISystemService system, IDiskTemperatureProvider temps) : PageModel
{
    public DashboardPageViewModel Dashboard { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        var poolsTask = zpool.GetAllPoolsAsync();
        var systemTask = system.GetSystemInfoAsync(zfs);
        var staticSystemTask = system.GetStaticSystemInfoAsync(zfs);

        await Task.WhenAll(poolsTask, systemTask, staticSystemTask);

        this.Dashboard = DashboardPresentationMapper.MapPage(
            poolsTask.Result,
            systemTask.Result,
            staticSystemTask.Result);
    }

    public async Task<IActionResult> OnGetLiveAsync()
    {
        var data = await system.GetDashboardDataAsync(zfs, zpool);
        ApplyTemperatures(data.DiskIoRates, temps.Temperatures);
        return new JsonResult(DashboardPresentationMapper.MapLive(data));
    }

    private static void ApplyTemperatures(List<DiskIoRateInfo> disks, IReadOnlyDictionary<string, int> temperatures)
    {
        for (var i = 0; i < disks.Count; i++)
        {
            if (temperatures.TryGetValue(disks[i].Device, out var temp))
                disks[i] = disks[i] with { Temperature = temp };
        }
    }
}

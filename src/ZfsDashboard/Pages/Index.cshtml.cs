using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;
using ZfsDashboard.Services;

namespace ZfsDashboard.Pages;

public class IndexModel(IZfsService zfs, IZpoolService zpool, ISystemService system, IDiskTemperatureProvider temps, IPartialRenderer renderer) : PageModel
{
    public List<Pool> Pools { get; private set; } = [];
    public SystemInfo SystemInfo { get; private set; } = new();
    public StaticSystemInfo StaticSystemInfo { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var poolsTask = zpool.GetAllPoolsAsync();
        var systemTask = system.GetSystemInfoAsync(zfs);
        var staticSystemTask = system.GetStaticSystemInfoAsync(zfs);

        await Task.WhenAll(poolsTask, systemTask, staticSystemTask);

        this.Pools = poolsTask.Result;
        this.SystemInfo = systemTask.Result;
        this.StaticSystemInfo = staticSystemTask.Result;
    }

    public async Task<IActionResult> OnGetLiveFragmentsAsync()
    {
        var data = await system.GetDashboardDataAsync(zfs, zpool);
        var temperatures = temps.Temperatures;
        ApplyTemperatures(data.DiskIoRates, temperatures);

        var ctx = this.PageContext;

        var netTask = renderer.RenderAsync(ctx, "_NetTable", data.NetworkRates);
        var diskTask = renderer.RenderAsync(ctx, "_DiskTable", data.DiskIoRates);

        var arcHitRateTask = data.Arc.MaxSize > 0
            ? renderer.RenderAsync(ctx, "_ArcHitRate", data.Arc) : null;
        var l2HitRateTask = data.Arc.MaxSize > 0 && data.Arc.L2Size > 0
            ? renderer.RenderAsync(ctx, "_L2HitRate", data.Arc) : null;

        var poolTasks = data.PoolDiskIoRates
            .Select(p => (p.PoolName, Task: renderer.RenderAsync(ctx, "_PoolDisks", p.Disks)))
            .ToList();
        var scrubTasks = data.PoolScrubs
            .Select(kv => (kv.Key, Task: renderer.RenderAsync(ctx, "_ScrubStatus", kv.Value)))
            .ToList();

        var optionals = new Task?[] { arcHitRateTask, l2HitRateTask }.OfType<Task>();
        await Task.WhenAll(
            new Task[] { netTask, diskTask }
                .Concat(optionals)
                .Concat(poolTasks.Select(x => (Task)x.Task))
                .Concat(scrubTasks.Select(x => (Task)x.Task)));

        var pools = poolTasks.ToDictionary(x => x.PoolName, x => x.Task.Result);
        var scrubs = scrubTasks.ToDictionary(x => x.Key, x => x.Task.Result);

        return new JsonResult(new
        {
            text = data.Text,
            net = netTask.Result,
            disk = diskTask.Result,
            pools,
            scrubs,
            arcHitRate = arcHitRateTask?.Result,
            l2HitRate = l2HitRateTask?.Result,
            networkRates = data.NetworkRates,
            diskIoRates = data.DiskIoRates,
        });
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

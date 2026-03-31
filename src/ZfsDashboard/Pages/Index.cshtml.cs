using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;
using ZfsDashboard.Services;

namespace ZfsDashboard.Pages;

public class IndexModel(IZfsService zfs, IZpoolService zpool, ISystemService system, IPartialRenderer renderer) : PageModel
{
    public List<Pool> Pools { get; private set; } = [];
    public int DatasetCount { get; private set; }
    public int SnapshotCount { get; private set; }
    public int ZVolCount { get; private set; }
    public string ZfsVersion { get; private set; } = "";
    public ArcStats Arc { get; private set; } = new();
    public ulong TotalSize { get; private set; }
    public ulong TotalUsed { get; private set; }
    public ulong TotalFree { get; private set; }
    public int HealthyPools { get; private set; }
    public int DegradedPools { get; private set; }
    public SystemInfo SystemInfo { get; private set; } = new();
    public MemoryInfo Memory { get; private set; } = new();
    public double CpuUsagePercent { get; private set; }

    public async Task OnGetAsync()
    {
        var poolsTask = zpool.GetAllPoolsAsync();
        var versionTask = zfs.GetZfsVersionAsync();
        var arcTask = zfs.GetArcStatsAsync();
        var systemTask = system.GetSystemInfoAsync();
        var memoryTask = system.GetMemoryInfoAsync();
        var cpuTask = system.GetCpuUsagePercentAsync();
        var zvolTask = zfs.GetAllZVolsAsync();

        await Task.WhenAll(poolsTask, versionTask, arcTask, systemTask, memoryTask, cpuTask, zvolTask);

        this.Pools = poolsTask.Result;
        this.ZfsVersion = versionTask.Result;
        this.Arc = arcTask.Result;
        this.SystemInfo = systemTask.Result;
        this.Memory = memoryTask.Result;
        this.CpuUsagePercent = cpuTask.Result;
        this.ZVolCount = zvolTask.Result.Count;

        var poolNames = this.Pools.Select(p => p.Name).ToArray();
        var datasetResults = await Task.WhenAll(poolNames.Select(n => zfs.GetDatasetsAsync(n)));
        var snapResults = await Task.WhenAll(poolNames.Select(n => zfs.GetSnapshotsAsync(n)));

        this.DatasetCount = datasetResults.Sum(d => d.Count);
        this.SnapshotCount = snapResults.Sum(s => s.Count);

        foreach (var pool in this.Pools)
        {
            this.TotalSize += pool.UsableSize;
            this.TotalUsed += pool.UsableUsed;
            this.TotalFree += pool.UsableAvail;

            if (pool.Health == "ONLINE")
                this.HealthyPools++;
            else
                this.DegradedPools++;
        }
    }

    public async Task<IActionResult> OnGetLiveFragmentsAsync()
    {
        var data = await system.GetDashboardDataAsync(zfs, zpool);
        var ctx = this.PageContext;

        var netTask  = renderer.RenderAsync(ctx, "_NetTable",  data.NetworkRates);
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
                .Concat(poolTasks.Select(x  => (Task)x.Task))
                .Concat(scrubTasks.Select(x => (Task)x.Task)));

        var pools  = poolTasks.ToDictionary(x  => x.PoolName, x => x.Task.Result);
        var scrubs = scrubTasks.ToDictionary(x => x.Key,      x => x.Task.Result);

        return new JsonResult(new
        {
            text = data.Text,
            net  = netTask.Result,
            disk = diskTask.Result,
            pools,
            scrubs,
            arcHitRate = arcHitRateTask?.Result,
            l2HitRate  = l2HitRateTask?.Result,
            networkRates = data.NetworkRates,
            diskIoRates  = data.DiskIoRates,
        });
    }
}

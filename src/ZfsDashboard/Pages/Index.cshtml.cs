using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;

namespace ZfsDashboard.Pages;

public class IndexModel(IZfsService zfs, IZpoolService zpool, SystemService system) : PageModel
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
}

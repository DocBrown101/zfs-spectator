using Zfs.Core.Models;
using ZfsDashboard.ViewModels.Shared;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record DashboardLiveViewModel
{
    public DashboardLiveViewModel(DashboardData data)
    {
        var disksByPool = data.PoolDiskIoRates.ToDictionary(
            group => group.PoolName,
            group => (IReadOnlyList<DiskIoRateViewModel>)group.Disks.Select(rate => new DiskIoRateViewModel(rate)).ToList());

        var poolNames = disksByPool.Keys
            .Concat(data.PoolScrubs.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        this.Uptime = data.System.Uptime;
        this.CpuUsagePercent = data.System.CpuUsagePercent;
        this.Memory = new MemoryCardViewModel(data.System.Memory);
        this.Arc = new ArcCardViewModel(data.System.Arc);
        this.NetworkRates = data.NetworkRates.Select(rate => new NetworkRateViewModel(rate)).ToList();
        this.DiskIoRates = data.DiskIoRates.Select(rate => new DiskIoRateViewModel(rate)).ToList();
        this.Pools = poolNames.Select(name => new PoolLiveViewModel(
            name,
            disksByPool.GetValueOrDefault(name) ?? [],
            new ScrubStatusViewModel(data.PoolScrubs.GetValueOrDefault(name, ScrubInfo.Idle)))).ToList();
    }

    public string Uptime { get; }
    public double CpuUsagePercent { get; }
    public MemoryCardViewModel Memory { get; }
    public ArcCardViewModel Arc { get; }
    public IReadOnlyList<NetworkRateViewModel> NetworkRates { get; }
    public IReadOnlyList<DiskIoRateViewModel> DiskIoRates { get; }
    public IReadOnlyList<PoolLiveViewModel> Pools { get; }
}

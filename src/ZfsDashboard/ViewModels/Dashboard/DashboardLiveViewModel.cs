using Zfs.Core.Models;
using ZfsDashboard.Services;
using ZfsDashboard.ViewModels.Shared;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record DashboardLiveViewModel
{
    public DashboardLiveViewModel(DashboardSnapshot snapshot)
        : this(snapshot.Data, snapshot.Pools)
    {
    }

    private DashboardLiveViewModel(
        DashboardData data,
        IReadOnlyList<Pool> pools)
    {
        var poolsByName = pools.ToDictionary(pool => pool.Name, StringComparer.Ordinal);
        var disksByPool = data.PoolDiskIoRates.ToDictionary(
            group => group.PoolName,
            group => (IReadOnlyList<DiskIoRateViewModel>)group.Disks.Select(rate => new DiskIoRateViewModel(rate)).ToList());

        var poolNames = disksByPool.Keys
            .Concat(data.PoolScrubs.Keys)
            .Concat(poolsByName.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        this.Uptime = data.System.Uptime;
        this.CpuUsagePercent = data.System.CpuUsagePercent;
        this.CpuTemperatureCelsius = data.System.CpuTemperatureCelsius;
        this.CpuTemperatureCss = CpuCardViewModel.TemperatureCssFor(data.System.CpuTemperatureCelsius);
        this.CpuTemperatureText = CpuCardViewModel.FormatTemperature(data.System.CpuTemperatureCelsius);
        this.Memory = new MemoryCardViewModel(data.System.Memory);
        this.Arc = new ArcCardViewModel(data.System.Arc);
        this.NetworkRates = data.NetworkRates.Select(rate => new NetworkRateViewModel(rate)).ToList();
        this.NetworkDownloadBytesPerSecond = data.NetworkRates.Sum(rate => rate.RxBytesPerSec);
        this.NetworkUploadBytesPerSecond = data.NetworkRates.Sum(rate => rate.TxBytesPerSec);
        this.DiskIoRates = data.DiskIoRates.Select(rate => new DiskIoRateViewModel(rate)).ToList();
        this.DiskReadBytesPerSecond = data.DiskIoRates.Sum(disk => disk.ReadBytesPerSec);
        this.DiskWriteBytesPerSecond = data.DiskIoRates.Sum(disk => disk.WriteBytesPerSec);
        this.Pools = poolNames.Select(name => new PoolLiveViewModel(
            name,
            disksByPool.GetValueOrDefault(name) ?? [],
            new ScrubStatusViewModel(data.PoolScrubs.GetValueOrDefault(name, ScrubInfo.Idle)),
            poolsByName.TryGetValue(name, out var pool) ? new PoolCardViewModel(pool) : null)).ToList();
        this.PoolCount = poolNames.Count;
    }

    public string Uptime { get; }
    public double CpuUsagePercent { get; }
    public double? CpuTemperatureCelsius { get; }
    public string CpuTemperatureCss { get; }
    public string CpuTemperatureText { get; }
    public MemoryCardViewModel Memory { get; }
    public ArcCardViewModel Arc { get; }
    public IReadOnlyList<NetworkRateViewModel> NetworkRates { get; }
    public double NetworkDownloadBytesPerSecond { get; }
    public double NetworkUploadBytesPerSecond { get; }
    public IReadOnlyList<DiskIoRateViewModel> DiskIoRates { get; }
    public double DiskReadBytesPerSecond { get; }
    public double DiskWriteBytesPerSecond { get; }
    public IReadOnlyList<PoolLiveViewModel> Pools { get; }
    public int PoolCount { get; }
}

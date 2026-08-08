using Zfs.Core.Models;
using ZfsDashboard.Services;
using ZfsDashboard.ViewModels.Shared;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record DashboardLiveViewModel
{
    private readonly DashboardData data;

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

        this.data = data;
        this.Memory = new MemoryCardViewModel(data.System.Memory);
        this.Arc = new ArcCardViewModel(data.System.Arc);
        this.NetworkRates = data.NetworkRates.Select(rate => new NetworkRateViewModel(rate)).ToList();
        this.DiskIoRates = data.DiskIoRates.Select(rate => new DiskIoRateViewModel(rate)).ToList();
        this.Pools = poolNames.Select(name => new PoolLiveViewModel(
            name,
            disksByPool.GetValueOrDefault(name) ?? [],
            new ScrubStatusViewModel(data.PoolScrubs.GetValueOrDefault(name, ScrubInfo.Idle)),
            poolsByName.TryGetValue(name, out var pool) ? new PoolCardViewModel(pool) : null)).ToList();
    }

    public string Uptime => this.data.System.Uptime;
    public double CpuUsagePercent => this.data.System.CpuUsagePercent;
    public double? CpuTemperatureCelsius => this.data.System.CpuTemperatureCelsius;
    public string CpuTemperatureCss => CpuCardViewModel.TemperatureCssFor(this.data.System.CpuTemperatureCelsius);
    public string CpuTemperatureText => CpuCardViewModel.FormatTemperature(this.data.System.CpuTemperatureCelsius);
    public MemoryCardViewModel Memory { get; }
    public ArcCardViewModel Arc { get; }
    public IReadOnlyList<NetworkRateViewModel> NetworkRates { get; }
    public double NetworkDownloadBytesPerSecond => this.NetworkRates.Sum(rate => rate.RxBytesPerSecond);
    public double NetworkUploadBytesPerSecond => this.NetworkRates.Sum(rate => rate.TxBytesPerSecond);
    public IReadOnlyList<DiskIoRateViewModel> DiskIoRates { get; }
    public double DiskReadBytesPerSecond => this.DiskIoRates.Sum(disk => disk.ReadBytesPerSecond);
    public double DiskWriteBytesPerSecond => this.DiskIoRates.Sum(disk => disk.WriteBytesPerSecond);
    public IReadOnlyList<PoolLiveViewModel> Pools { get; }
    public int PoolCount => this.Pools.Count;
}

using ZfsDashboard.ViewModels.Shared;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record PoolLiveViewModel(
    string Name,
    IReadOnlyList<DiskIoRateViewModel> Disks,
    ScrubStatusViewModel Scrub);

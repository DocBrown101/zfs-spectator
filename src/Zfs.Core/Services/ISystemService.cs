using Zfs.Core.Models;

namespace Zfs.Core.Services;

public interface ISystemService
{
    Task<DashboardData> GetDashboardDataAsync(
        IZfsService zfs,
        IZpoolService zpool,
        CancellationToken cancellationToken = default);
    Task<DashboardData> GetDashboardDataAsync(
        IZfsService zfs,
        IReadOnlyList<(Pool Pool, ScrubInfo Scrub)> poolSnapshots,
        CancellationToken cancellationToken = default);
    Task<StaticSystemInfo> GetStaticSystemInfoAsync(IZfsService zfs, CancellationToken cancellationToken = default);
    Task<SystemInfo> GetSystemInfoAsync(IZfsService zfs, CancellationToken cancellationToken = default);
}

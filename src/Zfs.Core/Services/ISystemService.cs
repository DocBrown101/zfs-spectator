using Zfs.Core.Models;

namespace Zfs.Core.Services;

public interface ISystemService
{
    Task<DashboardData> GetDashboardDataAsync(IZfsService zfs, IZpoolService zpool);
    Task<StaticSystemInfo> GetStaticSystemInfoAsync(IZfsService zfs);
    Task<SystemInfo> GetSystemInfoAsync(IZfsService zfs);
}

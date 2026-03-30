using Zfs.Core.Models;

namespace Zfs.Core.Services;

public interface ISystemService
{
    Task<DashboardData> GetDashboardDataAsync(IZfsService zfs, IZpoolService zpool);
    Task<SystemInfo> GetSystemInfoAsync();
    Task<MemoryInfo> GetMemoryInfoAsync();
    Task<double> GetCpuUsagePercentAsync();
}

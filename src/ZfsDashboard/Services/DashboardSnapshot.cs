using Zfs.Core.Models;

namespace ZfsDashboard.Services;

public sealed record DashboardSnapshot(
    DashboardData Data,
    IReadOnlyList<Pool> Pools,
    StaticSystemInfo StaticSystem);

public interface IDashboardSnapshotProvider
{
    DashboardSnapshot? Current { get; }
    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task<StaticSystemInfo> GetStaticSystemInfoAsync(CancellationToken cancellationToken = default);
}

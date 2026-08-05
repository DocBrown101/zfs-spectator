using Zfs.Core.Models;

namespace ZfsDashboard.ViewModels.Pools;

public sealed record PoolDeviceGroupViewModel
{
    public required string Title { get; init; }
    public required string Icon { get; init; }
    public required IReadOnlyList<PoolDevice> Devices { get; init; }
    public ulong Size { get; init; }
    public ulong Allocated { get; init; }
    public ulong Free { get; init; }
    public double UsagePercent => this.Size > 0 ? (double)this.Allocated / this.Size * 100 : 0;
}

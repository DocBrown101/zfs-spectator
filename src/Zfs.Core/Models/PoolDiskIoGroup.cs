namespace Zfs.Core.Models;

public record PoolDiskIoGroup
{
    public string PoolName { get; init; } = "";
    public List<DiskIoRateInfo> Disks { get; init; } = [];
}

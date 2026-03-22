namespace Zfs.Core.Models;

public record Snapshot
{
    public required string Name { get; init; }
    public required string DatasetName { get; init; }
    public required string SnapName { get; init; }
    public ulong Used { get; init; }
    public ulong Refer { get; init; }
    public DateTimeOffset Creation { get; init; }
}

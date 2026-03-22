namespace Zfs.Core.Models;

public record IoStats
{
    public required string PoolName { get; init; }
    public string ReadOps { get; init; } = "0";
    public string WriteOps { get; init; } = "0";
    public string ReadBw { get; init; } = "0";
    public string WriteBw { get; init; } = "0";
}

/// <summary>
/// Raw cumulative I/O counters from a single <c>zpool iostat -Hp</c> call.
/// </summary>
public record PoolIoSnapshot(string Name, ulong ReadOps, ulong WriteOps, ulong ReadBytes, ulong WriteBytes);

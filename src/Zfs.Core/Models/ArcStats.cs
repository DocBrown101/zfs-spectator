namespace Zfs.Core.Models;

public record ArcStats
{
    public ulong Size { get; init; }
    public ulong MaxSize { get; init; }
    public ulong Hits { get; init; }
    public ulong Misses { get; init; }
    public ulong L2Hits { get; init; }
    public ulong L2Misses { get; init; }
    public ulong L2Size { get; init; }
    public ulong MruSize { get; init; }
    public ulong MfuSize { get; init; }
    public ulong MetadataSize { get; init; }
    public ulong DataSize { get; init; }
}

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

    public double HitRate => (this.Hits + this.Misses) > 0 ? (double)this.Hits / (this.Hits + this.Misses) * 100 : 0;
    public double L2HitRate => (this.L2Hits + this.L2Misses) > 0 ? (double)this.L2Hits / (this.L2Hits + this.L2Misses) * 100 : 0;
    public double UsagePercent => this.MaxSize > 0 ? (double)this.Size / this.MaxSize * 100 : 0;
}

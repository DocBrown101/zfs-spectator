namespace Zfs.Core.Models;

public record DiskIoRateInfo
{
    public string Device { get; init; } = "";
    public string VdevType { get; init; } = "";
    public double ReadBytesPerSec { get; init; }
    public double WriteBytesPerSec { get; init; }
    public double ReadLatencyMs { get; init; }
    public double WriteLatencyMs { get; init; }
    public double QueueDepth { get; init; }
    public double UtilizationPct { get; init; }
    public int? Temperature { get; init; }
}

namespace Zfs.Core.Models;

public record VdevLatencyInfo
{
    public string DevicePath { get; init; } = "";
    public string DeviceName { get; init; } = "";
    public string Role { get; init; } = "";
    public double ReadLatencyMs { get; init; }
    public double WriteLatencyMs { get; init; }
    public double ReadOpsPerSec { get; init; }
    public double WriteOpsPerSec { get; init; }
    public double ReadBytesPerSec { get; init; }
    public double WriteBytesPerSec { get; init; }
    public double QueueDepth { get; init; }
    public double UtilizationPct { get; init; }

    public static string ShortenDeviceName(string name)
    {
        if (name.Length <= 15) return name;
        var partIdx = name.LastIndexOf("-part", StringComparison.Ordinal);
        if (partIdx > 0)
        {
            var suffix = name[partIdx..];
            var prefix = name[..partIdx];
            return prefix.Length > 10 ? $"{prefix[..4]}..{prefix[^4..]}{suffix}" : name;
        }
        return $"{name[..8]}..{name[^6..]}";
    }
}

public record PoolLatencyData
{
    public string PoolName { get; init; } = "";
    public List<VdevLatencyInfo> Devices { get; init; } = [];
}

/// <summary>
/// Raw cumulative counters for a single vdev from <c>zpool iostat -vlHp</c>.
/// </summary>
public record VdevCumulativeSnapshot(
    string DevicePath, string Role,
    double ReadOps, double WriteOps, double ReadBytes, double WriteBytes,
    double TotalWaitReadNs, double TotalWaitWriteNs,
    double DiskWaitReadNs, double DiskWaitWriteNs);

public record PoolVdevCumulativeData
{
    public string PoolName { get; init; } = "";
    public List<VdevCumulativeSnapshot> Devices { get; init; } = [];
}

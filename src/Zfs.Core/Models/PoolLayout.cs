namespace Zfs.Core.Models;

public record PoolLayout
{
    public string VdevType { get; init; } = "stripe";
    public string Operation { get; init; } = "";
    public List<PoolDevice> DataDevices { get; init; } = [];
    public List<PoolDevice> CacheDevices { get; init; } = [];
    public List<PoolDevice> LogDevices { get; init; } = [];
    public List<PoolDevice> SpareDevices { get; init; } = [];
    public List<PoolDevice> SpecialDevices { get; init; } = [];
    public long PoolErrorsRead { get; init; }
    public long PoolErrorsWrite { get; init; }
    public long PoolErrorsChecksum { get; init; }
}

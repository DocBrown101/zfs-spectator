namespace Zfs.Core.Models;

public record PoolLayout
{
    public string VdevType { get; init; } = "stripe";
    public string Operation { get; init; } = "";
    public IReadOnlyList<PoolDevice> DataDevices { get; init; } = [];
    public IReadOnlyList<PoolDevice> CacheDevices { get; init; } = [];
    public IReadOnlyList<PoolDevice> LogDevices { get; init; } = [];
    public IReadOnlyList<PoolDevice> SpareDevices { get; init; } = [];
    public IReadOnlyList<PoolDevice> SpecialDevices { get; init; } = [];
    public long PoolErrorsRead { get; init; }
    public long PoolErrorsWrite { get; init; }
    public long PoolErrorsChecksum { get; init; }

    public Pool ApplyTo(Pool pool) =>
        pool with
        {
            VdevType = this.VdevType,
            Operation = this.Operation,
            DataDevices = this.DataDevices,
            CacheDevices = this.CacheDevices,
            LogDevices = this.LogDevices,
            SpareDevices = this.SpareDevices,
            SpecialDevices = this.SpecialDevices,
            ErrorsRead = this.PoolErrorsRead,
            ErrorsWrite = this.PoolErrorsWrite,
            ErrorsChecksum = this.PoolErrorsChecksum,
        };
}

namespace Zfs.Core.Models;

public record Pool
{
    public required string Name { get; init; }
    public ulong Size { get; init; }
    public ulong Alloc { get; init; }
    public ulong Free { get; init; }
    public ulong UsableSize { get; init; }
    public ulong UsableUsed { get; init; }
    public ulong UsableAvail { get; init; }
    public required string Health { get; init; }
    public required string VdevType { get; init; }
    public required string Operation { get; init; }
    public int Ashift { get; init; }
    public required string Compression { get; init; }
    public required string CompRatio { get; init; }
    public required string Dedup { get; init; }
    public required string Sync { get; init; }
    public required string Atime { get; init; }
    public bool Encrypted { get; init; }
    public bool KeyLocked { get; init; }
    public string EncryptionAlgorithm { get; init; } = "";
    public List<PoolDevice> DataDevices { get; init; } = [];
    public List<PoolDevice> CacheDevices { get; init; } = [];
    public List<PoolDevice> LogDevices { get; init; } = [];
    public List<PoolDevice> SpareDevices { get; init; } = [];
    public List<PoolDevice> SpecialDevices { get; init; } = [];
    public ulong SpecialSize { get; init; }
    public ulong SpecialAlloc { get; init; }
    public ulong SpecialFree { get; init; }
    public int Fragmentation { get; init; }
    public long ErrorsRead { get; init; }
    public long ErrorsWrite { get; init; }
    public long ErrorsChecksum { get; init; }

    public double UsagePercent => this.UsableSize > 0 ? (double)this.UsableUsed / this.UsableSize * 100 : 0;
    public bool HasErrors => this.ErrorsRead > 0 || this.ErrorsWrite > 0 || this.ErrorsChecksum > 0;
}

public record PoolDevice
{
    public required string Path { get; init; }
    public required string Role { get; init; }
    public required string Status { get; init; }
    public bool Present { get; init; }
    public long ErrorsRead { get; init; }
    public long ErrorsWrite { get; init; }
    public long ErrorsChecksum { get; init; }
    public bool HasErrors => this.ErrorsRead > 0 || this.ErrorsWrite > 0 || this.ErrorsChecksum > 0;
}

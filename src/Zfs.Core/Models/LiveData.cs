namespace Zfs.Core.Models;

public record DashboardData
{
    public SystemInfo System { get; init; } = new();
    public List<NetworkRateInfo> NetworkRates { get; init; } = [];
    public List<DiskIoRateInfo> DiskIoRates { get; init; } = [];
    public List<PoolDiskIoGroup> PoolDiskIoRates { get; init; } = [];
    public Dictionary<string, ScrubInfo> PoolScrubs { get; init; } = [];
}

public record NetworkRateInfo
{
    public string Name { get; init; } = "";
    public double RxBytesPerSec { get; init; }
    public double TxBytesPerSec { get; init; }
}

public record StaticSystemInfo
{
    private const string Unknown = "unknown";

    public string Hostname { get; init; } = Unknown;
    public string Kernel { get; init; } = Unknown;
    public string ZfsVersion { get; init; } = Unknown;
    public string Processor { get; init; } = Unknown;
    public int PhysicalCoreCount { get; init; } = -1;
    public int LogicalCoreCount { get; init; } = -1;
}

public record SystemInfo
{
    public string Uptime { get; init; } = "";
    public ArcStats Arc { get; init; } = new();
    public MemoryInfo Memory { get; init; } = new();
    public double CpuUsagePercent { get; init; }
    public double? CpuTemperatureCelsius { get; init; }
}

public record MemoryInfo
{
    public ulong Total { get; init; }
    public ulong Available { get; init; }
    public ulong Used { get; init; }
    public ulong Buffers { get; init; }
    public ulong Cached { get; init; }
    public ulong SwapTotal { get; init; }
    public ulong SwapUsed { get; init; }
}

public record NetworkInterfaceInfo
{
    public string Name { get; init; } = "";
    public ulong RxBytes { get; init; }
    public ulong TxBytes { get; init; }
}

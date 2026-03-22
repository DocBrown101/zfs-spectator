namespace Zfs.Core.Models;

public record DashboardData
{
    public Dictionary<string, string> Text { get; init; } = new();
    public Dictionary<string, string> Html { get; init; } = new();
    public List<NetworkRateInfo> NetworkRates { get; init; } = new();
    public List<DiskIoRateInfo> DiskIoRates { get; init; } = new();
}

public record DiskIoRateInfo
{
    public string Device { get; init; } = "";
    public double ReadBytesPerSec { get; init; }
    public double WriteBytesPerSec { get; init; }
    public double ReadOpsPerSec { get; init; }
    public double WriteOpsPerSec { get; init; }
    public ulong IoInProgress { get; init; }
}

public record NetworkRateInfo
{
    public string Name { get; init; } = "";
    public double RxBytesPerSec { get; init; }
    public double TxBytesPerSec { get; init; }
}

public record SystemInfo
{
    public string Hostname { get; init; } = "";
    public string Kernel { get; init; } = "";
    public string Processor { get; init; } = "";
    public int CpuCount { get; init; }
    public string Uptime { get; init; } = "";
    public double UptimeSeconds { get; init; }
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
    public ulong SwapFree { get; init; }
    public double UsagePercent => this.Total > 0 ? (double)this.Used / this.Total * 100 : 0;
    public double SwapUsagePercent => this.SwapTotal > 0 ? (double)this.SwapUsed / this.SwapTotal * 100 : 0;
}

public record NetworkInterfaceInfo
{
    public string Name { get; init; } = "";
    public ulong RxBytes { get; init; }
    public ulong TxBytes { get; init; }
}

public record DiskIoInfo
{
    public string Device { get; init; } = "";
    public ulong ReadsCompleted { get; init; }
    public ulong WritesCompleted { get; init; }
    public ulong SectorsRead { get; init; }
    public ulong SectorsWritten { get; init; }
    public ulong ReadTimeMs { get; init; }
    public ulong WriteTimeMs { get; init; }
    public ulong IoInProgress { get; init; }
    public ulong IoTimeMs { get; init; }
}


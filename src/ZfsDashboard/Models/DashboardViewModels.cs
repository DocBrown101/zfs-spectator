namespace ZfsDashboard.Models;

public sealed record DashboardPageViewModel
{
    public required string Uptime { get; init; }
    public required CpuCardViewModel Cpu { get; init; }
    public required MemoryCardViewModel Memory { get; init; }
    public required ArcCardViewModel Arc { get; init; }
    public required IReadOnlyList<PoolCardViewModel> Pools { get; init; }
}

public sealed record MetricRowViewModel(
    string Label,
    string Value,
    string? ElementId = null,
    bool IsVisible = true);

public sealed record CpuCardViewModel
{
    public double UsagePercent { get; init; }
    public required IReadOnlyList<MetricRowViewModel> Details { get; init; }
}

public sealed record MemoryCardViewModel
{
    public double UsagePercent { get; init; }
    public required IReadOnlyList<MetricRowViewModel> Details { get; init; }
}

public sealed record ArcCardViewModel
{
    public bool IsVisible { get; init; }
    public double UsagePercent { get; init; }
    public double HitRate { get; init; }
    public required string HitRateCss { get; init; }
    public double? L2HitRate { get; init; }
    public string? L2HitRateCss { get; init; }
    public string? L2Size { get; init; }
    public required IReadOnlyList<MetricRowViewModel> Details { get; init; }
}

public sealed record PoolCardViewModel
{
    public required string Name { get; init; }
    public required string Health { get; init; }
    public required string HealthCss { get; init; }
    public bool Encrypted { get; init; }
    public required string EncryptionAlgorithm { get; init; }
    public bool HasErrors { get; init; }
    public required string ErrorTooltip { get; init; }
    public required string Size { get; init; }
    public required string Allocated { get; init; }
    public required string Free { get; init; }
    public double UsagePercent { get; init; }
}

public sealed record DashboardLiveResponse
{
    public required string Uptime { get; init; }
    public double CpuUsagePercent { get; init; }
    public required MemoryCardViewModel Memory { get; init; }
    public required ArcCardViewModel Arc { get; init; }
    public required IReadOnlyList<NetworkRateViewModel> NetworkRates { get; init; }
    public required IReadOnlyList<DiskIoRateViewModel> DiskIoRates { get; init; }
    public required IReadOnlyList<PoolLiveViewModel> Pools { get; init; }
}

public sealed record NetworkRateViewModel
{
    public required string Name { get; init; }
    public double RxBytesPerSecond { get; init; }
    public double TxBytesPerSecond { get; init; }
    public required string DownloadRate { get; init; }
    public required string UploadRate { get; init; }
}

public sealed record DiskIoRateViewModel
{
    public required string Device { get; init; }
    public required string VdevType { get; init; }
    public double ReadBytesPerSecond { get; init; }
    public double WriteBytesPerSecond { get; init; }
    public required string ReadRate { get; init; }
    public required string WriteRate { get; init; }
    public required string QueueDepth { get; init; }
    public required string ReadLatency { get; init; }
    public required string WriteLatency { get; init; }
    public double UtilizationPercent { get; init; }
    public required string UtilizationCss { get; init; }
    public int? Temperature { get; init; }
    public required string TemperatureCss { get; init; }
}

public sealed record PoolLiveViewModel
{
    public required string Name { get; init; }
    public required IReadOnlyList<DiskIoRateViewModel> Disks { get; init; }
    public required ScrubStatusViewModel Scrub { get; init; }
}

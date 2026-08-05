using Zfs.Core.Models;
using ZfsDashboard.Models;

namespace ZfsDashboard.Presentation;

public static class DashboardPresentationMapper
{
    public static DashboardPageViewModel MapPage(
        IReadOnlyList<Pool> pools,
        SystemInfo system,
        StaticSystemInfo staticSystem)
    {
        return new DashboardPageViewModel
        {
            Uptime = system.Uptime,
            Cpu = new CpuCardViewModel
            {
                UsagePercent = system.CpuUsagePercent,
                Details =
                [
                    new("Processor", staticSystem.Processor),
                    new("CPU Count", staticSystem.CpuCount.ToString()),
                ],
            },
            Memory = MapMemoryCard(system.Memory),
            Arc = MapArcCard(system.Arc),
            Pools = pools.Select(MapPoolCard).ToList(),
        };
    }

    public static DashboardLiveResponse MapLive(DashboardData data)
    {
        var system = data.System;
        var diskRates = data.DiskIoRates.Select(MapDiskRate).ToList();
        var disksByPool = data.PoolDiskIoRates.ToDictionary(
            group => group.PoolName,
            group => (IReadOnlyList<DiskIoRateViewModel>)group.Disks.Select(MapDiskRate).ToList());

        var poolNames = disksByPool.Keys
            .Concat(data.PoolScrubs.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new DashboardLiveResponse
        {
            Uptime = system.Uptime,
            CpuUsagePercent = system.CpuUsagePercent,
            Memory = MapMemoryCard(system.Memory),
            Arc = MapArcCard(system.Arc),
            NetworkRates = data.NetworkRates.Select(MapNetworkRate).ToList(),
            DiskIoRates = diskRates,
            Pools = poolNames.Select(name => new PoolLiveViewModel
            {
                Name = name,
                Disks = disksByPool.GetValueOrDefault(name) ?? [],
                Scrub = ScrubPresentationMapper.Map(data.PoolScrubs.GetValueOrDefault(name, ScrubInfo.Idle)),
            }).ToList(),
        };
    }

    private static MemoryCardViewModel MapMemoryCard(MemoryInfo memory) => new()
    {
        UsagePercent = memory.UsagePercent,
        Details = MapMemoryRows(memory),
    };

    private static IReadOnlyList<MetricRowViewModel> MapMemoryRows(MemoryInfo memory) =>
    [
        new("Total Memory", memory.Total.FormatBytes(), "memTotal"),
        new("Available Memory", memory.Available.FormatBytes(), "memAvail"),
        new("Used Memory", memory.Used.FormatBytes(), "memUsed"),
        new("Buffers / Cached", $"{memory.Buffers.FormatBytes()} / {memory.Cached.FormatBytes()}", "memBuffersCached"),
        new("Swap Used", $"{memory.SwapUsed.FormatBytes()} / {memory.SwapTotal.FormatBytes()}", "swapUsed"),
        new("Swap Usage", $"{memory.SwapUsagePercent:F1} %", "swapPct"),
    ];

    private static ArcCardViewModel MapArcCard(ArcStats arc)
    {
        var details = new List<MetricRowViewModel>
        {
            new("ARC Size", $"{arc.Size.FormatBytes()} / {arc.MaxSize.FormatBytes()}", "arcSize"),
        };

        details.Add(new("Metadata", arc.MetadataSize.FormatBytes(), "arcMeta", arc.MetadataSize > 0));
        details.Add(new("Data", arc.DataSize.FormatBytes(), "arcData", arc.DataSize > 0));
        details.Add(new(
            "MRU / MFU",
            $"{arc.MruSize.FormatBytes()} / {arc.MfuSize.FormatBytes()}",
            "arcMruMfu",
            arc.MruSize > 0 || arc.MfuSize > 0));

        return new ArcCardViewModel
        {
            IsVisible = arc.MaxSize > 0,
            UsagePercent = arc.UsagePercent,
            HitRate = arc.HitRate,
            HitRateCss = ArcHitRateCss(arc.HitRate),
            L2HitRate = arc.L2Size > 0 ? arc.L2HitRate : null,
            L2HitRateCss = arc.L2Size > 0 ? L2HitRateCss(arc.L2HitRate) : null,
            L2Size = arc.L2Size > 0 ? arc.L2Size.FormatBytes() : null,
            Details = details,
        };
    }

    private static PoolCardViewModel MapPoolCard(Pool pool) => new()
    {
        Name = pool.Name,
        Health = pool.Health,
        HealthCss = pool.Health.ToStatusBadgeCss(),
        Encrypted = pool.Encrypted,
        EncryptionAlgorithm = pool.EncryptionAlgorithm,
        HasErrors = pool.HasErrors,
        ErrorTooltip = $"R:{pool.ErrorsRead} W:{pool.ErrorsWrite} C:{pool.ErrorsChecksum}",
        Size = pool.UsableSize.FormatBytes(),
        Allocated = pool.UsableUsed.FormatBytes(),
        Free = pool.UsableAvail.FormatBytes(),
        UsagePercent = pool.UsagePercent,
    };

    private static NetworkRateViewModel MapNetworkRate(NetworkRateInfo rate) => new()
    {
        Name = rate.Name,
        RxBytesPerSecond = rate.RxBytesPerSec,
        TxBytesPerSecond = rate.TxBytesPerSec,
        DownloadRate = rate.RxBytesPerSec.FormatRate(),
        UploadRate = rate.TxBytesPerSec.FormatRate(),
    };

    private static DiskIoRateViewModel MapDiskRate(DiskIoRateInfo rate) => new()
    {
        Device = rate.Device,
        VdevType = rate.VdevType,
        ReadBytesPerSecond = rate.ReadBytesPerSec,
        WriteBytesPerSecond = rate.WriteBytesPerSec,
        ReadRate = rate.ReadBytesPerSec.FormatRate(),
        WriteRate = rate.WriteBytesPerSec.FormatRate(),
        QueueDepth = Math.Round(rate.QueueDepth).ToString(),
        ReadLatency = FormatLatency(rate.ReadLatencyMs),
        WriteLatency = FormatLatency(rate.WriteLatencyMs),
        UtilizationPercent = rate.UtilizationPct,
        UtilizationCss = rate.UtilizationPct > 80 ? "text-danger" : rate.UtilizationPct > 50 ? "text-warning" : "",
        Temperature = rate.Temperature,
        TemperatureCss = rate.Temperature >= 50 ? "text-danger" : rate.Temperature >= 40 ? "text-warning" : "",
    };

    private static string FormatLatency(double value) => value <= 0 ? "\u2013" : value < 10 ? value.ToString("F2") : value.ToString("F1");
    private static string ArcHitRateCss(double percentage) => percentage >= 90 ? "text-success" : percentage >= 70 ? "text-warning" : "text-danger";
    private static string L2HitRateCss(double percentage) => percentage >= 70 ? "text-success" : "text-warning";
}

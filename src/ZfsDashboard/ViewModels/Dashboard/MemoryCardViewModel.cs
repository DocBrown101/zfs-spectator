using Zfs.Core.Models;
using ZfsDashboard.Presentation;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record MemoryCardViewModel
{
    public MemoryCardViewModel(MemoryInfo memory)
    {
        this.UsagePercent = memory.UsagePercent;
        this.Details =
        [
            new("Total Memory", memory.Total.FormatBytes(), "memTotal"),
            new("Available Memory", memory.Available.FormatBytes(), "memAvail"),
            new("Used Memory", memory.Used.FormatBytes(), "memUsed"),
            new("Buffers / Cached", $"{memory.Buffers.FormatBytes()} / {memory.Cached.FormatBytes()}", "memBuffersCached"),
            new("Swap Used", $"{memory.SwapUsed.FormatBytes()} / {memory.SwapTotal.FormatBytes()}", "swapUsed"),
            new("Swap Usage", $"{memory.SwapUsagePercent:F1} %", "swapPct"),
        ];
    }

    public double UsagePercent { get; }
    public IReadOnlyList<MetricRowViewModel> Details { get; }
}

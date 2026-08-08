using Zfs.Core.Models;
using ZfsDashboard.Presentation;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record MemoryCardViewModel
{
    public MemoryCardViewModel(MemoryInfo memory)
    {
        var usagePercent = memory.Total > 0 ? (double)memory.Used / memory.Total * 100 : 0;
        var swapUsagePercent = memory.SwapTotal > 0 ? (double)memory.SwapUsed / memory.SwapTotal * 100 : 0;

        this.UsagePercent = usagePercent;
        this.Details =
        [
            new("Total Memory", memory.Total.FormatBytes(), "memTotal"),
            new("Available Memory", memory.Available.FormatBytes(), "memAvail"),
            new("Used Memory", memory.Used.FormatBytes(), "memUsed"),
            new("Buffers / Cached", $"{memory.Buffers.FormatBytes()} / {memory.Cached.FormatBytes()}", "memBuffersCached"),
            new("Swap Used", $"{memory.SwapUsed.FormatBytes()} / {memory.SwapTotal.FormatBytes()}", "swapUsed"),
            new("Swap Usage", $"{swapUsagePercent:F1} %", "swapPct"),
        ];
    }

    public double UsagePercent { get; }
    public IReadOnlyList<MetricRowViewModel> Details { get; }
}

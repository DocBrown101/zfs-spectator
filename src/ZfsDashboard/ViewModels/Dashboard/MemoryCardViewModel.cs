using Zfs.Core.Models;
using ZfsDashboard.Presentation;
using ZfsDashboard.ViewModels.Shared;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record MemoryCardViewModel
{
    private readonly MemoryInfo memory;

    public MemoryCardViewModel(MemoryInfo memory)
    {
        this.memory = memory;
    }

    public double UsagePercent =>
        this.memory.Total > 0 ? (double)this.memory.Used / this.memory.Total * 100 : 0;

    private double SwapUsagePercent =>
        this.memory.SwapTotal > 0 ? (double)this.memory.SwapUsed / this.memory.SwapTotal * 100 : 0;

    public IReadOnlyList<KeyValueRowViewModel> Details =>
    [
        new("Total Memory", this.memory.Total.FormatBytes(), "memTotal"),
        new("Available Memory", this.memory.Available.FormatBytes(), "memAvail"),
        new("Used Memory", this.memory.Used.FormatBytes(), "memUsed"),
        new("Buffers / Cached", $"{this.memory.Buffers.FormatBytes()} / {this.memory.Cached.FormatBytes()}", "memBuffersCached"),
        new("Swap Used", $"{this.memory.SwapUsed.FormatBytes()} / {this.memory.SwapTotal.FormatBytes()}", "swapUsed"),
        new("Swap Usage", $"{this.SwapUsagePercent:F1} %", "swapPct"),
    ];
}

using Zfs.Core.Models;
using ZfsDashboard.ViewModels.Shared;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record CpuCardViewModel
{
    public CpuCardViewModel(SystemInfo system, StaticSystemInfo staticSystem)
    {
        this.UsagePercent = system.CpuUsagePercent;
        this.Details =
        [
            new("Processor", staticSystem.Processor),
            new("CPU Count", staticSystem.CpuCount > 0 ? staticSystem.CpuCount.ToString() : "unknown"),
        ];
    }

    public double UsagePercent { get; }
    public IReadOnlyList<KeyValueRowViewModel> Details { get; }
}

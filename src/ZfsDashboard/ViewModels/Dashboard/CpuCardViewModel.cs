using Zfs.Core.Models;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record CpuCardViewModel
{
    public CpuCardViewModel(SystemInfo system, StaticSystemInfo staticSystem)
    {
        this.UsagePercent = system.CpuUsagePercent;
        this.Details =
        [
            new("Processor", staticSystem.Processor),
            new("CPU Count", staticSystem.CpuCount.ToString()),
        ];
    }

    public double UsagePercent { get; }
    public IReadOnlyList<MetricRowViewModel> Details { get; }
}

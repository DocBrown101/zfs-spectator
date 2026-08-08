using System.Globalization;
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
            new("Cores (physical / logical)", $"{FormatCoreCount(staticSystem.PhysicalCoreCount)} / {FormatCoreCount(staticSystem.LogicalCoreCount)}"),
            new("Temperature", FormatTemperature(system.CpuTemperatureCelsius), "cpuTemperature", ValueCss: TemperatureCssFor(system.CpuTemperatureCelsius)),
        ];
    }

    public double UsagePercent { get; }
    public IReadOnlyList<KeyValueRowViewModel> Details { get; }

    internal static string TemperatureCssFor(double? temperature)
    {
        if (temperature >= 95) return "text-danger";
        if (temperature >= 80) return "text-warning";
        return "";
    }

    private static string FormatCoreCount(int count) =>
        count > 0 ? count.ToString(CultureInfo.InvariantCulture) : "unknown";

    private static string FormatTemperature(double? celsius) =>
        celsius is { } value ? $"{value.ToString("F1", CultureInfo.InvariantCulture)} °C" : "N/A";
}

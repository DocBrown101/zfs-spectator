using System.Globalization;
using Zfs.Core.Models;
using ZfsDashboard.ViewModels.Shared;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed class CpuCardViewModel(SystemInfo system, StaticSystemInfo staticSystem)
{
    public double UsagePercent => system.CpuUsagePercent;

    public IReadOnlyList<KeyValueRowViewModel> Details { get; } =
    [
        new("Processor", staticSystem.Processor),
        new("Cores (physical / logical)", $"{FormatCoreCount(staticSystem.PhysicalCoreCount)} / {FormatCoreCount(staticSystem.LogicalCoreCount)}"),
        new("Temperature", FormatTemperature(system.CpuTemperatureCelsius), "cpuTemperature", ValueCss: TemperatureCssFor(system.CpuTemperatureCelsius)),
    ];

    private static string TemperatureCssFor(double? temperature) =>
        temperature switch
        {
            >= 95 => "text-danger",
            >= 80 => "text-warning",
            _ => "",
        };

    private static string FormatCoreCount(int count) => count > 0 ? count.ToString(CultureInfo.InvariantCulture) : "unknown";

    internal static string FormatTemperature(double? celsius) => celsius is { } value ? $"{value.ToString("F1", CultureInfo.InvariantCulture)} °C" : "N/A";
}

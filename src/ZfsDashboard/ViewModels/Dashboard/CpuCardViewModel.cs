using System.Globalization;
using Zfs.Core.Models;
using ZfsDashboard.ViewModels.Shared;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record CpuCardViewModel
{
    private readonly SystemInfo system;
    private readonly StaticSystemInfo staticSystem;

    public CpuCardViewModel(SystemInfo system, StaticSystemInfo staticSystem)
    {
        this.system = system;
        this.staticSystem = staticSystem;
    }

    public double UsagePercent => this.system.CpuUsagePercent;

    public IReadOnlyList<KeyValueRowViewModel> Details =>
    [
        new("Processor", this.staticSystem.Processor),
        new("Cores (physical / logical)", $"{FormatCoreCount(this.staticSystem.PhysicalCoreCount)} / {FormatCoreCount(this.staticSystem.LogicalCoreCount)}"),
        new("Temperature", FormatTemperature(this.system.CpuTemperatureCelsius), "cpuTemperature", ValueCss: TemperatureCssFor(this.system.CpuTemperatureCelsius)),
    ];

    internal static string TemperatureCssFor(double? temperature)
    {
        if (temperature >= 95) return "text-danger";
        if (temperature >= 80) return "text-warning";
        return "";
    }

    private static string FormatCoreCount(int count) =>
        count > 0 ? count.ToString(CultureInfo.InvariantCulture) : "unknown";

    internal static string FormatTemperature(double? celsius) =>
        celsius is { } value ? $"{value.ToString("F1", CultureInfo.InvariantCulture)} °C" : "N/A";
}

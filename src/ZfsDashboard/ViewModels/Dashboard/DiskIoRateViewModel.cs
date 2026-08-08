using System.Globalization;
using Zfs.Core.Models;
using ZfsDashboard.Presentation;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record DiskIoRateViewModel
{
    private readonly DiskIoRateInfo rate;

    public DiskIoRateViewModel(DiskIoRateInfo rate)
    {
        this.rate = rate;
    }

    public string Device => this.rate.Device;
    public string VdevType => this.rate.VdevType;
    public double ReadBytesPerSecond => this.rate.ReadBytesPerSec;
    public double WriteBytesPerSecond => this.rate.WriteBytesPerSec;
    public string ReadRate => this.rate.ReadBytesPerSec.FormatRate();
    public string WriteRate => this.rate.WriteBytesPerSec.FormatRate();
    public string QueueDepth => Math.Round(this.rate.QueueDepth).ToString();
    public string ReadLatency => FormatLatency(this.rate.ReadLatencyMs);
    public string WriteLatency => FormatLatency(this.rate.WriteLatencyMs);
    public double UtilizationPercent => this.rate.UtilizationPct;
    public string UtilizationPercentText => $"{this.UtilizationPercent.ToString("F1", CultureInfo.InvariantCulture)}%";
    public string UtilizationCss => UtilizationCssFor(this.rate.UtilizationPct);
    public int? Temperature => this.rate.Temperature;
    public string TemperatureText => this.Temperature is { } value ? $"{value.ToString(CultureInfo.InvariantCulture)} °C" : "\u2013";
    public string TemperatureCss => TemperatureCssFor(this.rate.Temperature);

    private static string UtilizationCssFor(double utilization)
    {
        if (utilization > 80) return "text-danger";
        if (utilization > 50) return "text-warning";
        return "";
    }

    private static string TemperatureCssFor(int? temperature)
    {
        if (temperature >= 50) return "text-danger";
        if (temperature >= 40) return "text-warning";
        return "";
    }

    private static string FormatLatency(double value)
    {
        if (value <= 0) return "\u2013";
        return value < 10 ? value.ToString("F2") : value.ToString("F1");
    }
}

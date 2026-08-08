using Zfs.Core.Models;
using ZfsDashboard.Presentation;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record DiskIoRateViewModel
{
    public DiskIoRateViewModel(DiskIoRateInfo rate)
    {
        this.Device = rate.Device;
        this.VdevType = rate.VdevType;
        this.ReadBytesPerSecond = rate.ReadBytesPerSec;
        this.WriteBytesPerSecond = rate.WriteBytesPerSec;
        this.ReadRate = rate.ReadBytesPerSec.FormatRate();
        this.WriteRate = rate.WriteBytesPerSec.FormatRate();
        this.QueueDepth = Math.Round(rate.QueueDepth).ToString();
        this.ReadLatency = FormatLatency(rate.ReadLatencyMs);
        this.WriteLatency = FormatLatency(rate.WriteLatencyMs);
        this.UtilizationPercent = rate.UtilizationPct;
        this.UtilizationCss = UtilizationCssFor(rate.UtilizationPct);
        this.Temperature = rate.Temperature;
        this.TemperatureCss = TemperatureCssFor(rate.Temperature);
    }

    public string Device { get; }
    public string VdevType { get; }
    public double ReadBytesPerSecond { get; }
    public double WriteBytesPerSecond { get; }
    public string ReadRate { get; }
    public string WriteRate { get; }
    public string QueueDepth { get; }
    public string ReadLatency { get; }
    public string WriteLatency { get; }
    public double UtilizationPercent { get; }
    public string UtilizationCss { get; }
    public int? Temperature { get; }
    public string TemperatureCss { get; }

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

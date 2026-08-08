using System.Globalization;

namespace ZfsDashboard.Presentation;

public static class FormatExtensions
{
    private static readonly string[] Units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB"];
    private static readonly string[] RateUnits = ["B/s", "KiB/s", "MiB/s", "GiB/s"];

    public static string FormatBytes(this ulong bytes)
    {
        if (bytes == 0) return "0 B";

        var i = 0;
        var size = (double)bytes;
        while (size >= 1024 && i < Units.Length - 1) { size /= 1024; i++; }
        return $"{size.ToString("0.##", CultureInfo.InvariantCulture)} {Units[i]}";
    }

    public static string FormatRate(this double bytesPerSec)
    {
        if (bytesPerSec <= 0) return "0 B/s";

        var i = 0;
        var size = bytesPerSec;
        while (size >= 1024 && i < RateUnits.Length - 1) { size /= 1024; i++; }
        return $"{size.ToString("0.00", CultureInfo.InvariantCulture)} {RateUnits[i]}";
    }

    public static string FormatBytesOrNone(this ulong bytes) => bytes == 0 ? "none" : bytes.FormatBytes();

    public static string ToStatusBadgeCss(this string status) => status switch
    {
        "ONLINE" => "text-bg-success",
        "DEGRADED" => "text-bg-warning",
        "FAULTED" or "UNAVAIL" => "text-bg-danger",
        "OFFLINE" or "REMOVED" => "text-bg-secondary",
        _ => "text-bg-secondary",
    };

    public static string ToCapacityCss(this double percentage) => percentage > 85 ? "bg-danger" : percentage > 70 ? "bg-warning" : "bg-success";
}

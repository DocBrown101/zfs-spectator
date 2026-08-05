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
        "ONLINE" => "bg-success",
        "DEGRADED" => "bg-warning text-dark",
        "FAULTED" or "UNAVAIL" => "bg-danger",
        "OFFLINE" or "REMOVED" => "bg-secondary",
        _ => "bg-secondary",
    };
}

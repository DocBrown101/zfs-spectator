using System.Globalization;

namespace Zfs.Core.Services;

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
        var s = bytesPerSec;
        while (s >= 1024 && i < RateUnits.Length - 1) { s /= 1024; i++; }
        return $"{s.ToString("0.00", CultureInfo.InvariantCulture)} {RateUnits[i]}";
    }

    public static string FormatUptime(this double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1)
            return $"{ts.Hours}h {ts.Minutes}m {ts.Seconds}s";
        return $"{ts.Minutes}m {ts.Seconds}s";
    }

    public static string FormatBytesOrNone(this ulong bytes) => bytes == 0 ? "none" : bytes.FormatBytes();

    public static string FormatOpsRate(double opsPerSec) => opsPerSec switch
    {
        0 => "0",
        < 10 => opsPerSec.ToString("0.#", CultureInfo.InvariantCulture),
        _ => FormatNumber((ulong)opsPerSec),
    };

    public static string FormatNumber(ulong n) => n switch
    {
        >= 1_000_000_000 => $"{(n / 1_000_000_000.0).ToString("0.00", CultureInfo.InvariantCulture)}G",
        >= 1_000_000 => $"{(n / 1_000_000.0).ToString("0.00", CultureInfo.InvariantCulture)}M",
        >= 1_000 => $"{(n / 1_000.0).ToString("0.0", CultureInfo.InvariantCulture)}K",
        _ => n.ToString(),
    };

    public static string ToStatusBadgeCss(this string status) => status switch
    {
        "ONLINE" => "bg-success",
        "DEGRADED" => "bg-warning text-dark",
        "FAULTED" or "UNAVAIL" => "bg-danger",
        "OFFLINE" or "REMOVED" => "bg-secondary",
        _ => "bg-secondary",
    };
}

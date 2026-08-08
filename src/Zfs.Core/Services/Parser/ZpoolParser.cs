namespace Zfs.Core.Services.Parser;

using System.Globalization;
using System.Text.Json;
using Zfs.Core.Models;

public static class ZpoolParser
{
    private const string StripeVdevType = "stripe";
    private const string VdevsProperty = "vdevs";
    private const string StateProperty = "state";

    // ── Pool Listing (from zpool list -Hpj) ─────────────────────────────

    public static List<Pool> ParsePools(string json, bool requireOutput = false)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        using var pools = TryGetPoolContainer(json, "zpool list", null, requireOutput);
        if (pools is null) return [];

        var result = new List<Pool>();
        foreach (var poolEntry in pools.Value.EnumerateObject())
        {
            var pool = poolEntry.Value;
            if (pool.ValueKind != JsonValueKind.Object ||
                string.IsNullOrEmpty(JsonHelper.GetString(pool, "name")) ||
                !pool.TryGetProperty("properties", out var props) ||
                props.ValueKind != JsonValueKind.Object ||
                (requireOutput && !HasValidListFields(props)))
            {
                if (requireOutput) throw new InvalidOperationException($"zpool list returned incomplete data for {poolEntry.Name}");
                continue;
            }

            var (specialSize, specialAlloc, specialFree) = ParseSpecialVdevSizes(pool);

            result.Add(new Pool
            {
                Name = JsonHelper.GetString(pool, "name"),
                Size = JsonHelper.GetPropertyUlong(props, "size"),
                Alloc = JsonHelper.GetPropertyUlong(props, "allocated"),
                Free = JsonHelper.GetPropertyUlong(props, "free"),
                Health = JsonHelper.GetPropertyString(props, "health"),
                Fragmentation = JsonHelper.GetPropertyInt(props, "fragmentation"),
                SpecialSize = specialSize,
                SpecialAlloc = specialAlloc,
                SpecialFree = specialFree,
                VdevType = StripeVdevType,
                Operation = "",
                Compression = "lz4",
                CompRatio = "1.00x",
                Dedup = "off",
                Sync = "standard",
                Atime = "off",
            });
        }
        return result;
    }

    private static bool HasValidListFields(JsonElement props)
        => JsonHelper.TryGetPropertyValue(props, "size", out var size) && ulong.TryParse(size, out _) &&
           JsonHelper.TryGetPropertyValue(props, "allocated", out var allocated) && ulong.TryParse(allocated, out _) &&
           JsonHelper.TryGetPropertyValue(props, "free", out var free) && ulong.TryParse(free, out _) &&
           JsonHelper.TryGetPropertyValue(props, "health", out _) &&
           JsonHelper.TryGetPropertyValue(props, "fragmentation", out var fragmentation) && int.TryParse(fragmentation, out _);

    // ── Ashift (from zpool get -Hpj ashift) ─────────────────────────────

    public static int ParseAshift(string json, string poolName, bool requireOutput = false)
    {
        using var pool = TryGetPoolContainer(json, "zpool get ashift", poolName, requireOutput);
        if (pool is null) return 0;

        if (!pool.Value.TryGetProperty("properties", out var props))
        {
            if (requireOutput) throw new InvalidOperationException($"zpool get ashift returned incomplete data for {poolName}");
            return 0;
        }

        if (requireOutput &&
            (!props.TryGetProperty("ashift", out var ashift) || !ashift.TryGetProperty("value", out _)))
            throw new InvalidOperationException($"zpool get ashift returned incomplete data for {poolName}");

        return JsonHelper.GetPropertyInt(props, "ashift");
    }

    // ── Pool Layout (from zpool status -Pj) ─────────────────────────────

    public static PoolLayout ParsePoolLayout(string json, string poolName, bool requireOutput = false)
    {
        using var pool = TryGetPoolContainer(json, "zpool status", poolName, requireOutput);
        if (pool is null) return new PoolLayout();

        if (requireOutput &&
            (!pool.Value.TryGetProperty(VdevsProperty, out var checkedVdevs) ||
             checkedVdevs.ValueKind != JsonValueKind.Object ||
             !checkedVdevs.TryGetProperty(poolName, out var checkedRootVdev) ||
             checkedRootVdev.ValueKind != JsonValueKind.Object))
            throw new InvalidOperationException($"zpool status returned incomplete data for {poolName}");

        var operation = ParseOperation(pool.Value);
        var (dataDevices, vdevType, poolErrR, poolErrW, poolErrC) = ParseDataDevices(pool.Value, poolName);

        var logDevices = ParseSectionDevices(pool.Value, "logs", "log");
        if (logDevices.Count == 0)
            logDevices = ParseSectionDevices(pool.Value, "log", "log");

        var spareDevices = ParseSectionDevices(pool.Value, "spares", "spare");
        if (spareDevices.Count == 0)
            spareDevices = ParseSectionDevices(pool.Value, "spare", "spare");

        return new PoolLayout
        {
            VdevType = vdevType,
            Operation = operation,
            DataDevices = dataDevices,
            CacheDevices = ParseSectionDevices(pool.Value, "cache", "cache"),
            LogDevices = logDevices,
            SpareDevices = spareDevices,
            SpecialDevices = ParseSectionDevices(pool.Value, "special", "special"),
            PoolErrorsRead = poolErrR,
            PoolErrorsWrite = poolErrW,
            PoolErrorsChecksum = poolErrC,
        };
    }

    private static string ParseOperation(JsonElement pool)
    {
        if (!pool.TryGetProperty("scan_stats", out var scanStats)) return "";
        if (JsonHelper.GetString(scanStats, StateProperty) != "SCANNING") return "";

        return JsonHelper.GetString(scanStats, "function") switch
        {
            "SCRUB" => "scrubbing",
            "RESILVER" => "resilvering",
            _ => "",
        };
    }

    private static (List<PoolDevice> Devices, string VdevType, long ErrorsRead, long ErrorsWrite, long ErrorsChecksum) ParseDataDevices(
        JsonElement pool,
        string poolName)
    {
        var devices = new List<PoolDevice>();
        var vdevType = StripeVdevType;

        if (!pool.TryGetProperty(VdevsProperty, out var vdevs) ||
            !vdevs.TryGetProperty(poolName, out var rootVdev))
            return (devices, vdevType, 0, 0, 0);

        var errorsRead = JsonHelper.GetLong(rootVdev, "read_errors");
        var errorsWrite = JsonHelper.GetLong(rootVdev, "write_errors");
        var errorsChecksum = JsonHelper.GetLong(rootVdev, "checksum_errors");

        if (!rootVdev.TryGetProperty(VdevsProperty, out var dataVdevs))
            return (devices, vdevType, errorsRead, errorsWrite, errorsChecksum);

        foreach (var vdevEntry in dataVdevs.EnumerateObject())
        {
            var vdev = vdevEntry.Value;
            var vdevTypeName = JsonHelper.GetString(vdev, "vdev_type");

            if (vdevTypeName == "disk")
            {
                devices.Add(CreateDevice(vdev, StripeVdevType));
            }
            else
            {
                var role = DetectVdevType(vdevEntry.Name);
                vdevType = role;

                if (vdev.TryGetProperty(VdevsProperty, out var groupDisks))
                {
                    foreach (var disk in groupDisks.EnumerateObject())
                        devices.Add(CreateDevice(disk.Value, role));
                }
            }
        }

        return (devices, vdevType, errorsRead, errorsWrite, errorsChecksum);
    }

    // ── Scrub Info (from zpool status -Pj) ──────────────────────────────

    public static ScrubInfo ParseScrubInfo(string json, string poolName)
    {
        using var pool = JsonHelper.TryGetObject(json, "pools", poolName);
        if (pool is null) return ScrubInfo.Idle;
        if (!pool.Value.TryGetProperty("scan_stats", out var scan)) return ScrubInfo.Idle;

        var function = JsonHelper.GetString(scan, "function");
        if (function is not ("SCRUB" or "RESILVER")) return ScrubInfo.Idle;

        return JsonHelper.GetString(scan, StateProperty) switch
        {
            "SCANNING" => CreateRunningScrubInfo(scan),
            "FINISHED" or "CANCELED" => CreateCompletedScrubInfo(scan),
            _ => ScrubInfo.Idle,
        };
    }

    private static ScrubInfo CreateCompletedScrubInfo(JsonElement scan)
    {
        var startTime = JsonHelper.GetString(scan, "start_time");
        var endTime = JsonHelper.GetString(scan, "end_time");
        return new ScrubInfo
        {
            State = JsonHelper.GetString(scan, StateProperty) == "FINISHED" ? "finished" : "canceled",
            Errors = JsonHelper.GetLong(scan, "errors"),
            StartTime = startTime,
            FinishTime = endTime,
            Duration = ComputeDuration(startTime, endTime),
        };
    }

    private static string ComputeDuration(string startTime, string endTime)
    {
        if (!TryParseScanTime(startTime, out var start) || !TryParseScanTime(endTime, out var end))
            return "";
        if (end <= start) return "";
        var elapsed = end - start;
        return $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    private static readonly (string Format, CultureInfo Culture)[] ScanTimeFormats =
    [
        ("ddd d. MMM HH:mm:ss yyyy", CultureInfo.GetCultureInfo("de-DE")),
        ("ddd MMM d HH:mm:ss yyyy", CultureInfo.GetCultureInfo("en-US")),
        ("ddd d MMM HH:mm:ss yyyy", CultureInfo.GetCultureInfo("en-US")),
    ];

    private static bool TryParseScanTime(string value, out DateTime result)
    {
        if (string.IsNullOrEmpty(value) || value == "-")
        {
            result = default;
            return false;
        }

        // zfs emits scan timestamps in the system locale (e.g. "Mi 27. Mär 12:54:52 CET 2024").
        // Strip the timezone abbreviation (the token right before the year); both timestamps
        // parse the same way, so the wall-clock difference is the duration zfs reports.
        // Without a timezone (e.g. "Wed Mar 27 12:54:52 2024") the token before the year is
        // the clock time and must be kept, so only drop alphabetic timezone tokens.
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 && parts[^1].Length == 4 && int.TryParse(parts[^1], out _) &&
            parts[^2].All(char.IsLetter))
            value = string.Join(' ', parts[..^2].Concat(parts[^1..]));

        foreach (var (format, culture) in ScanTimeFormats)
        {
            if (DateTime.TryParseExact(value, format, culture, DateTimeStyles.AllowWhiteSpaces, out result))
                return true;
        }

        result = default;
        return false;
    }

    // ── Scrub text parsing (from zpool status without -j) ───────────────

    public static string ParseScrubTimeLeft(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var match = RegexHelper.ScrubTimeLeft().Match(text);
        return match.Success ? match.Groups[1].Value : "";
    }

    private static (ulong Size, ulong Alloc, ulong Free) ParseSpecialVdevSizes(JsonElement pool)
    {
        if (!pool.TryGetProperty(VdevsProperty, out var vdevs)) return (0, 0, 0);
        if (!vdevs.TryGetProperty("special", out var special)) return (0, 0, 0);

        ulong totalSize = 0, totalAlloc = 0, totalFree = 0;

        foreach (var vdev in special.EnumerateObject())
        {
            if (!vdev.Value.TryGetProperty("properties", out var props)) continue;

            totalSize += JsonHelper.GetPropertyUlong(props, "size");
            totalAlloc += JsonHelper.GetPropertyUlong(props, "allocated");
            totalFree += JsonHelper.GetPropertyUlong(props, "free");
        }

        return (totalSize, totalAlloc, totalFree);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static JsonHelper.JsonObjectLease? TryGetPoolContainer(
        string json,
        string commandName,
        string? poolName,
        bool requireOutput)
    {
        JsonHelper.JsonObjectLease? result;
        try
        {
            result = JsonHelper.TryGetObject(json, "pools", poolName);
        }
        catch (JsonException ex)
        {
            if (!requireOutput) throw;
            throw new InvalidOperationException(
                poolName is null ? $"{commandName} returned invalid data" : $"{commandName} returned invalid data for {poolName}", ex);
        }

        if (result is null && requireOutput)
        {
            var poolSuffix = poolName is null ? "" : $" for {poolName}";
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(json)
                    ? $"{commandName} returned no data{poolSuffix}"
                    : $"{commandName} returned incomplete data{poolSuffix}");
        }

        return result;
    }

    private static List<PoolDevice> ParseSectionDevices(JsonElement pool, string sectionName, string role)
    {
        var devices = new List<PoolDevice>();
        if (!pool.TryGetProperty(sectionName, out var section)) return devices;

        foreach (var group in section.EnumerateObject().Select(groupEntry => groupEntry.Value))
        {
            var groupType = JsonHelper.GetString(group, "vdev_type");

            if (groupType == "disk")
            {
                devices.Add(CreateDevice(group, role));
            }
            else if (group.TryGetProperty(VdevsProperty, out var groupDisks))
            {
                foreach (var disk in groupDisks.EnumerateObject())
                    devices.Add(CreateDevice(disk.Value, role));
            }
        }
        return devices;
    }

    private static PoolDevice CreateDevice(JsonElement element, string role)
    {
        var path = JsonHelper.GetString(element, "path");
        if (path.Length == 0)
            path = JsonHelper.GetString(element, "name");

        return new PoolDevice
        {
            Path = path,
            VdevType = role,
            Status = JsonHelper.GetString(element, StateProperty),
            Present = false,
            ErrorsRead = JsonHelper.GetLong(element, "read_errors"),
            ErrorsWrite = JsonHelper.GetLong(element, "write_errors"),
            ErrorsChecksum = JsonHelper.GetLong(element, "checksum_errors"),
        };
    }

    private static ScrubInfo CreateRunningScrubInfo(JsonElement scan)
    {
        var toExamine = JsonHelper.ParseByteString(JsonHelper.GetString(scan, "to_examine"));
        var issued = JsonHelper.ParseByteString(JsonHelper.GetString(scan, "issued"));
        var progressPct = toExamine > 0 ? Math.Min(issued / toExamine * 100, 100) : 0;

        return new ScrubInfo
        {
            State = "running",
            StartTime = JsonHelper.GetString(scan, "start_time"),
            Errors = JsonHelper.GetLong(scan, "errors"),
            ProgressPct = Math.Round(progressPct, 2),
        };
    }

    private static string DetectVdevType(string name) => name switch
    {
        _ when name.StartsWith("mirror") => "mirror",
        _ when name.StartsWith("raidz3") => "raidz3",
        _ when name.StartsWith("raidz2") => "raidz2",
        _ when name.StartsWith("raidz") => "raidz1",
        _ when name.StartsWith("draid") => "draid",
        _ => StripeVdevType,
    };

}

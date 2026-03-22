namespace Zfs.Core.Services.Parser;

using System.Text.Json;
using Zfs.Core.Models;

public static class ZpoolParser
{
    // ── Pool Listing (from zpool list -Hpj) ─────────────────────────────

    public static List<Pool> ParsePools(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("pools", out var pools)) return [];

        var result = new List<Pool>();
        foreach (var poolEntry in pools.EnumerateObject())
        {
            var pool = poolEntry.Value;
            if (!pool.TryGetProperty("properties", out var props)) continue;

            result.Add(new Pool
            {
                Name = GetString(pool, "name"),
                Size = GetPropertyUlong(props, "size"),
                Alloc = GetPropertyUlong(props, "allocated"),
                Free = GetPropertyUlong(props, "free"),
                Health = GetPropertyString(props, "health"),
                Fragmentation = GetPropertyInt(props, "fragmentation"),
                VdevType = "stripe",
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

    // ── Pool Names (from zpool list -Hpj) ───────────────────────────────

    public static List<string> ParsePoolNames(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("pools", out var pools)) return [];

        return pools.EnumerateObject().Select(p => p.Name).ToList();
    }

    // ── Ashift (from zpool get -Hpj ashift) ─────────────────────────────

    public static int ParseAshift(string json, string poolName)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("pools", out var pools)) return 0;
        if (!pools.TryGetProperty(poolName, out var pool)) return 0;
        if (!pool.TryGetProperty("properties", out var props)) return 0;

        return GetPropertyInt(props, "ashift");
    }

    // ── Pool Layout (from zpool status -Pj) ─────────────────────────────

    public static PoolLayout ParsePoolLayout(string json, string poolName)
    {
        if (string.IsNullOrWhiteSpace(json)) return new PoolLayout();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("pools", out var pools)) return new PoolLayout();
        if (!pools.TryGetProperty(poolName, out var pool)) return new PoolLayout();

        var operation = "";
        if (pool.TryGetProperty("scan_stats", out var scanStats))
        {
            var function = GetString(scanStats, "function");
            var state = GetString(scanStats, "state");
            if (state == "SCANNING")
            {
                operation = function switch
                {
                    "SCRUB" => "scrubbing",
                    "RESILVER" => "resilvering",
                    _ => "",
                };
            }
        }

        long poolErrR = 0, poolErrW = 0, poolErrC = 0;
        var vdevType = "stripe";
        var dataDevices = new List<PoolDevice>();

        if (pool.TryGetProperty("vdevs", out var vdevs) &&
            vdevs.TryGetProperty(poolName, out var rootVdev))
        {
            poolErrR = GetLong(rootVdev, "read_errors");
            poolErrW = GetLong(rootVdev, "write_errors");
            poolErrC = GetLong(rootVdev, "checksum_errors");

            if (rootVdev.TryGetProperty("vdevs", out var dataVdevs))
            {
                foreach (var vdevEntry in dataVdevs.EnumerateObject())
                {
                    var vdev = vdevEntry.Value;
                    var vdevTypeName = GetString(vdev, "vdev_type");

                    if (vdevTypeName == "disk")
                    {
                        dataDevices.Add(CreateDevice(vdev, "stripe"));
                    }
                    else
                    {
                        var role = DetectVdevType(vdevEntry.Name);
                        vdevType = role;

                        if (vdev.TryGetProperty("vdevs", out var groupDisks))
                        {
                            foreach (var disk in groupDisks.EnumerateObject())
                                dataDevices.Add(CreateDevice(disk.Value, role));
                        }
                    }
                }
            }
        }

        var logDevices = ParseSectionDevices(pool, "logs", "log");
        if (logDevices.Count == 0)
            logDevices = ParseSectionDevices(pool, "log", "log");

        var spareDevices = ParseSectionDevices(pool, "spares", "spare");
        if (spareDevices.Count == 0)
            spareDevices = ParseSectionDevices(pool, "spare", "spare");

        return new PoolLayout
        {
            VdevType = vdevType,
            Operation = operation,
            DataDevices = dataDevices,
            CacheDevices = ParseSectionDevices(pool, "cache", "cache"),
            LogDevices = logDevices,
            SpareDevices = spareDevices,
            SpecialDevices = ParseSectionDevices(pool, "special", "special"),
            PoolErrorsRead = poolErrR,
            PoolErrorsWrite = poolErrW,
            PoolErrorsChecksum = poolErrC,
        };
    }

    // ── Scrub Info (from zpool status -Pj) ──────────────────────────────

    public static ScrubInfo ParseScrubInfo(string json, string poolName)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ScrubInfo { State = "idle" };

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("pools", out var pools)) return new ScrubInfo { State = "idle" };
        if (!pools.TryGetProperty(poolName, out var pool)) return new ScrubInfo { State = "idle" };
        if (!pool.TryGetProperty("scan_stats", out var scan)) return new ScrubInfo { State = "idle" };

        var function = GetString(scan, "function");
        if (function is not ("SCRUB" or "RESILVER")) return new ScrubInfo { State = "idle" };

        return GetString(scan, "state") switch
        {
            "FINISHED" => new ScrubInfo
            {
                State = "finished",
                Errors = GetLong(scan, "errors"),
                StartTime = GetString(scan, "start_time"),
                FinishTime = GetString(scan, "end_time"),
            },
            "SCANNING" => new ScrubInfo
            {
                State = "running",
                StartTime = GetString(scan, "start_time"),
                Errors = GetLong(scan, "errors"),
            },
            "CANCELED" => new ScrubInfo
            {
                State = "canceled",
                Errors = GetLong(scan, "errors"),
                StartTime = GetString(scan, "start_time"),
                FinishTime = GetString(scan, "end_time"),
            },
            _ => new ScrubInfo { State = "idle" },
        };
    }

    // ── Special VDEV Size (from zpool list -Hpvj) ───────────────────────

    public static (ulong Size, ulong Alloc, ulong Free) ParseSpecialVdevSize(string json, string poolName)
    {
        if (string.IsNullOrWhiteSpace(json)) return (0, 0, 0);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("pools", out var pools)) return (0, 0, 0);
        if (!pools.TryGetProperty(poolName, out var pool)) return (0, 0, 0);
        if (!pool.TryGetProperty("vdevs", out var vdevs)) return (0, 0, 0);
        if (!vdevs.TryGetProperty("special", out var special)) return (0, 0, 0);

        ulong totalSize = 0, totalAlloc = 0, totalFree = 0;

        foreach (var vdev in special.EnumerateObject())
        {
            if (!vdev.Value.TryGetProperty("properties", out var props)) continue;

            totalSize += GetPropertyUlong(props, "size");
            totalAlloc += GetPropertyUlong(props, "allocated");
            totalFree += GetPropertyUlong(props, "free");
        }

        return (totalSize, totalAlloc, totalFree);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static List<PoolDevice> ParseSectionDevices(JsonElement pool, string sectionName, string role)
    {
        var devices = new List<PoolDevice>();
        if (!pool.TryGetProperty(sectionName, out var section)) return devices;

        foreach (var groupEntry in section.EnumerateObject())
        {
            var group = groupEntry.Value;
            var groupType = GetString(group, "vdev_type");

            if (groupType == "disk")
            {
                devices.Add(CreateDevice(group, role));
            }
            else if (group.TryGetProperty("vdevs", out var groupDisks))
            {
                foreach (var disk in groupDisks.EnumerateObject())
                    devices.Add(CreateDevice(disk.Value, role));
            }
        }
        return devices;
    }

    private static PoolDevice CreateDevice(JsonElement element, string role)
    {
        var path = GetString(element, "path");
        if (path.Length == 0)
            path = GetString(element, "name");

        return new PoolDevice
        {
            Path = path,
            Role = role,
            Status = GetString(element, "state"),
            Present = false,
            ErrorsRead = GetLong(element, "read_errors"),
            ErrorsWrite = GetLong(element, "write_errors"),
            ErrorsChecksum = GetLong(element, "checksum_errors"),
        };
    }

    private static string DetectVdevType(string name)
    {
        if (name.StartsWith("mirror")) return "mirror";
        if (name.StartsWith("raidz3")) return "raidz3";
        if (name.StartsWith("raidz2")) return "raidz2";
        if (name.StartsWith("raidz")) return "raidz1";
        if (name.StartsWith("draid")) return "draid";
        return "stripe";
    }

    private static ulong GetPropertyUlong(JsonElement properties, string name)
    {
        if (!properties.TryGetProperty(name, out var prop)) return 0;
        if (!prop.TryGetProperty("value", out var val)) return 0;

        var raw = val.GetString();
        if (string.IsNullOrEmpty(raw) || raw == "-") return 0;

        return ulong.TryParse(raw, out var result) ? result : 0;
    }

    private static string GetPropertyString(JsonElement properties, string name)
    {
        if (!properties.TryGetProperty(name, out var prop)) return "";
        if (!prop.TryGetProperty("value", out var val)) return "";
        return val.GetString() ?? "";
    }

    private static int GetPropertyInt(JsonElement properties, string name)
    {
        if (!properties.TryGetProperty(name, out var prop)) return 0;
        if (!prop.TryGetProperty("value", out var val)) return 0;

        var raw = val.GetString();
        if (string.IsNullOrEmpty(raw) || raw == "-") return 0;

        return int.TryParse(raw, out var result) ? result : 0;
    }

    private static string GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var val)) return "";
        return val.GetString() ?? "";
    }

    private static long GetLong(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var val)) return 0;
        var raw = val.GetString();
        return long.TryParse(raw, out var result) ? result : 0;
    }
}

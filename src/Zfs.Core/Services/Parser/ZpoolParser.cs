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
                Name = JsonHelper.GetString(pool, "name"),
                Size = JsonHelper.GetPropertyUlong(props, "size"),
                Alloc = JsonHelper.GetPropertyUlong(props, "allocated"),
                Free = JsonHelper.GetPropertyUlong(props, "free"),
                Health = JsonHelper.GetPropertyString(props, "health"),
                Fragmentation = JsonHelper.GetPropertyInt(props, "fragmentation"),
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

        return JsonHelper.GetPropertyInt(props, "ashift");
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
            var function = JsonHelper.GetString(scanStats, "function");
            var state = JsonHelper.GetString(scanStats, "state");
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
            poolErrR = JsonHelper.GetLong(rootVdev, "read_errors");
            poolErrW = JsonHelper.GetLong(rootVdev, "write_errors");
            poolErrC = JsonHelper.GetLong(rootVdev, "checksum_errors");

            if (rootVdev.TryGetProperty("vdevs", out var dataVdevs))
            {
                foreach (var vdevEntry in dataVdevs.EnumerateObject())
                {
                    var vdev = vdevEntry.Value;
                    var vdevTypeName = JsonHelper.GetString(vdev, "vdev_type");

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

        var function = JsonHelper.GetString(scan, "function");
        if (function is not ("SCRUB" or "RESILVER")) return new ScrubInfo { State = "idle" };

        return JsonHelper.GetString(scan, "state") switch
        {
            "FINISHED" => new ScrubInfo
            {
                State = "finished",
                Errors = JsonHelper.GetLong(scan, "errors"),
                StartTime = JsonHelper.GetString(scan, "start_time"),
                FinishTime = JsonHelper.GetString(scan, "end_time"),
            },
            "SCANNING" => CreateRunningScrubInfo(scan),
            "CANCELED" => new ScrubInfo
            {
                State = "canceled",
                Errors = JsonHelper.GetLong(scan, "errors"),
                StartTime = JsonHelper.GetString(scan, "start_time"),
                FinishTime = JsonHelper.GetString(scan, "end_time"),
            },
            _ => new ScrubInfo { State = "idle" },
        };
    }

    // ── Scrub text parsing (from zpool status without -j) ───────────────

    public static string ParseScrubTimeLeft(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var match = RegexHelper.ScrubTimeLeft().Match(text);
        return match.Success ? match.Groups[1].Value : "";
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

            totalSize += JsonHelper.GetPropertyUlong(props, "size");
            totalAlloc += JsonHelper.GetPropertyUlong(props, "allocated");
            totalFree += JsonHelper.GetPropertyUlong(props, "free");
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
            var groupType = JsonHelper.GetString(group, "vdev_type");

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
        var path = JsonHelper.GetString(element, "path");
        if (path.Length == 0)
            path = JsonHelper.GetString(element, "name");

        return new PoolDevice
        {
            Path = path,
            Role = role,
            Status = JsonHelper.GetString(element, "state"),
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

    private static string DetectVdevType(string name)
    {
        if (name.StartsWith("mirror")) return "mirror";
        if (name.StartsWith("raidz3")) return "raidz3";
        if (name.StartsWith("raidz2")) return "raidz2";
        if (name.StartsWith("raidz")) return "raidz1";
        if (name.StartsWith("draid")) return "draid";
        return "stripe";
    }

    // ── VDEV I/O cumulative output (from zpool iostat -vlHp) ────────────

    /// <summary>
    /// Parses the tab-separated cumulative output of <c>zpool iostat -vlHp</c>
    /// into per-pool lists of vdev cumulative counters.
    /// </summary>
    public static List<PoolVdevCumulativeData> ParseVdevIostat(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return [];

        var allLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<PoolVdevCumulativeData>();
        var currentPool = "";
        var currentSection = "data";
        List<VdevCumulativeSnapshot>? currentDevices = null;

        foreach (var line in allLines)
        {
            var (indent, fields) = SplitIostatLine(line);
            if (fields.Count < 7) continue;

            var name = fields[0];

            // Indent 0 = pool name or section header (cache, log, special, …)
            if (indent == 0)
            {
                if (IsIostatSectionHeader(name))
                {
                    currentSection = NormalizeIostatSection(name);
                }
                else
                {
                    if (currentDevices != null)
                        result.Add(new PoolVdevCumulativeData { PoolName = currentPool, Devices = currentDevices });
                    currentPool = name;
                    currentSection = "data";
                    currentDevices = [];
                }
                continue;
            }

            // Skip group vdevs (mirror-0, raidz1-0, …) and placeholder dashes
            if (IsGroupVdev(name) || fields[3] == "-" || currentDevices == null) continue;

            var hasLatency = fields.Count > 10;
            currentDevices.Add(new VdevCumulativeSnapshot(
                DevicePath: name,
                Role: currentSection,
                ReadOps: ParseDouble(fields[3]),
                WriteOps: ParseDouble(fields[4]),
                ReadBytes: ParseDouble(fields[5]),
                WriteBytes: ParseDouble(fields[6]),
                TotalWaitReadNs: hasLatency ? ParseDouble(fields[7]) : 0,
                TotalWaitWriteNs: hasLatency ? ParseDouble(fields[8]) : 0,
                DiskWaitReadNs: hasLatency ? ParseDouble(fields[9]) : 0,
                DiskWaitWriteNs: hasLatency ? ParseDouble(fields[10]) : 0));
        }
        if (currentDevices != null)
            result.Add(new PoolVdevCumulativeData { PoolName = currentPool, Devices = currentDevices });

        return result;
    }

    /// <summary>
    /// Splits an iostat line into an indent level and logical fields
    /// (where fields[0] is always the name, fields[1] is alloc, etc.).
    /// Handles both tab-indented (<c>-H</c> flag) and space-indented formats.
    /// </summary>
    private static (int Indent, ArraySegment<string> Fields) SplitIostatLine(string line)
    {
        var raw = line.Split('\t');

        // Count leading empty fields produced by tab-based indentation
        int tabIndent = 0;
        while (tabIndent < raw.Length && raw[tabIndent].Length == 0)
            tabIndent++;

        if (tabIndent >= raw.Length)
            return (0, ArraySegment<string>.Empty);

        // Detect space-based indentation within the name field
        var trimmed = raw[tabIndent].TrimStart();
        var spaceIndent = raw[tabIndent].Length - trimmed.Length;
        if (spaceIndent > 0)
            raw[tabIndent] = trimmed;

        return (tabIndent > 0 ? tabIndent : spaceIndent,
                new ArraySegment<string>(raw, tabIndent, raw.Length - tabIndent));
    }

    private static bool IsIostatSectionHeader(string name) =>
        name is "cache" or "dedup" or "log" or "logs" or "special" or "spare" or "spares";

    private static bool IsGroupVdev(string name) =>
        name.StartsWith("mirror") || name.StartsWith("raidz") || name.StartsWith("draid");

    private static string NormalizeIostatSection(string name) => name switch
    {
        "logs" => "log",
        "spares" => "spare",
        _ => name,
    };

    private static double ParseDouble(string s) =>
        double.TryParse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
}

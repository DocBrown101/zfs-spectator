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
        if (string.IsNullOrWhiteSpace(json)) return ScrubInfo.Idle;

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("pools", out var pools)) return ScrubInfo.Idle;
        if (!pools.TryGetProperty(poolName, out var pool)) return ScrubInfo.Idle;
        if (!pool.TryGetProperty("scan_stats", out var scan)) return ScrubInfo.Idle;

        var function = JsonHelper.GetString(scan, "function");
        if (function is not ("SCRUB" or "RESILVER")) return ScrubInfo.Idle;

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
            _ => ScrubInfo.Idle,
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
        if (!doc.RootElement.TryGetProperty("pools", out var pools)) return (0, 0, 0);
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

    // Core fields always present in zpool iostat -v output:
    //   name(0), alloc(1), free(2), read_ops(3), write_ops(4), read_bw(5), write_bw(6)
    private const int IdxAlloc = 1;
    private const int IdxFree = 2;
    private const int IdxReadOps = 3;
    private const int IdxWriteOps = 4;
    private const int IdxReadBytes = 5;
    private const int IdxWriteBytes = 6;
    private const int CoreFieldCount = 7;
    private const int LatencyStart = CoreFieldCount;

    private enum LineRole { Pool, GroupVdev, SectionHeader, LeafDevice }

    /// <summary>
    /// Parses the tab-separated cumulative output of <c>zpool iostat -vlHp</c>
    /// into per-pool lists of vdev cumulative counters.
    /// Uses a depth stack to resolve the vdev hierarchy and dynamic field counting
    /// to handle variable numbers of latency columns across ZFS versions.
    /// Throws <see cref="FormatException"/> when non-empty output produces no devices.
    /// </summary>
    public static List<PoolVdevCumulativeData> ParseVdevIostat(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return [];

        var allLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<PoolVdevCumulativeData>();
        var skippedLines = new List<string>();

        string currentPool = "";
        string currentSection = "data";
        List<VdevCumulativeSnapshot>? currentDevices = null;

        bool hasIndentation = DetectHierarchicalIndentation(allLines);
        var depthStack = new Stack<int>();

        foreach (var line in allLines)
        {
            var (indent, fields) = SplitIostatLine(line);
            if (fields.Count < CoreFieldCount)
            {
                skippedLines.Add($"too few fields ({fields.Count}): {line.TrimEnd()}");
                continue;
            }

            var name = fields[0];
            var role = ClassifyLine(name, indent, fields, hasIndentation,
                                    insidePool: currentDevices != null, currentSection);

            switch (role)
            {
                case LineRole.SectionHeader:
                    currentSection = NormalizeIostatSection(name);
                    while (depthStack.Count > 1) depthStack.Pop();
                    break;

                case LineRole.Pool:
                    if (currentDevices != null)
                        result.Add(new PoolVdevCumulativeData { PoolName = currentPool, Devices = currentDevices });
                    currentPool = name;
                    currentSection = "data";
                    currentDevices = [];
                    depthStack.Clear();
                    depthStack.Push(indent);
                    break;

                case LineRole.GroupVdev:
                    while (depthStack.Count > 1 && depthStack.Peek() >= indent)
                        depthStack.Pop();
                    depthStack.Push(indent);
                    break;

                case LineRole.LeafDevice:
                    if (currentDevices == null)
                    {
                        skippedLines.Add($"device line outside any pool: {line.TrimEnd()}");
                        break;
                    }
                    if (fields[IdxReadOps] == "-") break;
                    currentDevices.Add(ExtractSnapshot(name, currentSection, fields));
                    break;
            }
        }
        if (currentDevices != null)
            result.Add(new PoolVdevCumulativeData { PoolName = currentPool, Devices = currentDevices });

        // Non-empty output must produce at least one pool with devices
        if (result.Count == 0 || result.All(p => p.Devices.Count == 0))
        {
            var poolInfo = result.Count > 0
                ? $"Detected pool(s): {string.Join(", ", result.Select(p => $"'{p.PoolName}' ({p.Devices.Count} devices)"))}"
                : "No pools detected.";
            var skippedInfo = skippedLines.Count > 0
                ? $"\nSkipped lines:\n{string.Join("\n", skippedLines.Select(s => $"  - {s}"))}"
                : "";
            var sampleLines = string.Join("\n", allLines.Take(10).Select((l, i) => $"  [{i}] {l.TrimEnd()}"));
            if (allLines.Length > 10)
                sampleLines += $"\n  ... ({allLines.Length - 10} more lines)";

            throw new FormatException(
                $"zpool iostat output ({allLines.Length} lines) could not be parsed into any pool with devices.\n" +
                $"{poolInfo}{skippedInfo}\n" +
                $"First lines:\n{sampleLines}");
        }

        return result;
    }

    /// <summary>
    /// Checks whether any data line in the output uses leading whitespace
    /// (tab or space) for hierarchical vdev indentation.
    /// Only considers lines with enough fields to be real data (not wrapped fragments).
    /// </summary>
    private static bool DetectHierarchicalIndentation(string[] lines)
    {
        foreach (var line in lines)
        {
            if (line.Length == 0 || (line[0] != '\t' && line[0] != ' '))
                continue;
            if (line.Split('\t').Length >= CoreFieldCount)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Determines the role of a parsed iostat line within the vdev hierarchy.
    /// With indentation the depth is unambiguous; in flat format (some ZFS versions
    /// omit tab-indent in -H mode) heuristics based on name patterns, alloc/free
    /// values, and current section are used.
    /// </summary>
    private static LineRole ClassifyLine(string name, int indent, ArraySegment<string> fields,
        bool hasIndentation, bool insidePool, string currentSection)
    {
        if (IsIostatSectionHeader(name))
            return LineRole.SectionHeader;

        if (IsGroupVdev(name))
            return LineRole.GroupVdev;

        // With hierarchical indentation, indent 0 = pool, indent > 0 = leaf device
        if (hasIndentation)
            return indent == 0 ? LineRole.Pool : LineRole.LeafDevice;

        // Flat format fallback: no indentation available
        if (!insidePool)
            return LineRole.Pool;

        if (fields[IdxAlloc].Trim() == "0" && fields[IdxFree].Trim() == "0")
            return LineRole.LeafDevice;

        if (currentSection != "data")
            return LineRole.LeafDevice;

        return LineRole.Pool;
    }

    /// <summary>
    /// Extracts a <see cref="VdevCumulativeSnapshot"/> from the parsed fields.
    /// Latency columns are detected dynamically: after the 7 core fields,
    /// each pair of values represents a latency category (total_wait, disk_wait,
    /// syncq_wait, asyncq_wait …). Only total_wait and disk_wait are captured;
    /// additional pairs are silently ignored.
    /// </summary>
    private static VdevCumulativeSnapshot ExtractSnapshot(
        string name, string section, ArraySegment<string> fields)
    {
        int latencyPairs = Math.Max((fields.Count - CoreFieldCount) / 2, 0);

        return new VdevCumulativeSnapshot(
            DevicePath: name,
            Role: section,
            ReadOps: ParseDouble(fields[IdxReadOps]),
            WriteOps: ParseDouble(fields[IdxWriteOps]),
            ReadBytes: ParseDouble(fields[IdxReadBytes]),
            WriteBytes: ParseDouble(fields[IdxWriteBytes]),
            TotalWaitReadNs: latencyPairs >= 1 ? ParseDouble(fields[LatencyStart]) : 0,
            TotalWaitWriteNs: latencyPairs >= 1 ? ParseDouble(fields[LatencyStart + 1]) : 0,
            DiskWaitReadNs: latencyPairs >= 2 ? ParseDouble(fields[LatencyStart + 2]) : 0,
            DiskWaitWriteNs: latencyPairs >= 2 ? ParseDouble(fields[LatencyStart + 3]) : 0);
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

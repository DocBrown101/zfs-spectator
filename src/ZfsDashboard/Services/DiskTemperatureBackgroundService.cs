using System.Collections.Frozen;
using Zfs.Core;
using Zfs.Core.Models;
using Zfs.Core.Services;

namespace ZfsDashboard.Services;

public sealed class DiskTemperatureBackgroundService(
    ICommandExecutor commandExecutor,
    IZpoolService zpoolService,
    ILogger<DiskTemperatureBackgroundService> logger) : BackgroundService, IDiskTemperatureProvider
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private volatile IReadOnlyDictionary<string, int> temperatures = FrozenDictionary<string, int>.Empty;

    public IReadOnlyDictionary<string, int> Temperatures => this.temperatures;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run once immediately, then every 60 seconds.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var outputTask = commandExecutor.ExecuteAsync("zpool", "iostat -c temp -H");
                var poolsTask = zpoolService.GetAllPoolsAsync();
                await Task.WhenAll(outputTask, poolsTask);

                var deviceMap = BuildDeviceMap(poolsTask.Result, logger);
                this.temperatures = ParseTemperatures(outputTask.Result, deviceMap, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to collect disk temperatures");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>
    /// Builds a mapping from ZFS device leaf names (e.g. "wwn-0x50014ee2b702ad1b")
    /// to physical disk names (e.g. "sda") using pool device paths and symlink resolution.
    /// </summary>
    internal static Dictionary<string, string> BuildDeviceMap(List<Pool> pools, ILogger<DiskTemperatureBackgroundService> logger)
    {
        var map = new Dictionary<string, string>();

        foreach (var pool in pools)
        {
            var allDevices = pool.DataDevices
                .Concat(pool.CacheDevices)
                .Concat(pool.LogDevices)
                .Concat(pool.SpareDevices)
                .Concat(pool.SpecialDevices);

            foreach (var dev in allDevices)
            {
                // Pool device path: e.g. "/dev/disk/by-id/wwn-0x50014ee2b702ad1b-part1"
                // or short name like "sda1"
                var physicalDisk = SystemService.ResolveToPhysicalDisk(dev.Path);

                // Extract the leaf name and strip partition suffixes to match iostat output.
                // "/dev/disk/by-id/wwn-0x50014ee2b702ad1b-part1" → "wwn-0x50014ee2b702ad1b"
                var leafName = StripByIdPartition(Path.GetFileName(dev.Path));

                if (physicalDisk != null)
                {
                    map.TryAdd(leafName, physicalDisk);
                }
                else if (dev.Path.Contains("/disk/by-"))
                {
                    // Fallback for by-id paths: use the identifier itself for matching iostat output.
                    // This allows temperatures to display even when symlink resolution fails.
                    map.TryAdd(leafName, leafName);
                    logger.LogWarning("Could not resolve device path to physical disk: {DevicePath}. Using by-id name as fallback.", dev.Path);
                }
            }
        }

        return map;
    }

    /// <summary>
    /// Strips the "-partN" suffix from a by-id device name.
    /// <c>wwn-0x50014ee2b702ad1b-part1</c> → <c>wwn-0x50014ee2b702ad1b</c>.
    /// </summary>
    internal static string StripByIdPartition(string name)
    {
        var idx = name.LastIndexOf("-part", StringComparison.Ordinal);
        if (idx > 0 && idx < name.Length - 5)
        {
            var suffix = name[(idx + 5)..];
            if (suffix.Length > 0 && suffix.All(char.IsDigit))
                return name[..idx];
        }

        return name;
    }

    /// <summary>
    /// Parses the output of <c>zpool iostat -c temp -H</c>.
    /// Each line is tab-separated; the device name is in the first column
    /// and the temperature value is the last column.
    /// Pool/vdev summary lines lack a temperature column and are skipped.
    /// </summary>
    internal static IReadOnlyDictionary<string, int> ParseTemperatures(
        string output, IReadOnlyDictionary<string, string> deviceMap, ILogger<DiskTemperatureBackgroundService> logger)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            logger.LogWarning("Empty output!");
            return FrozenDictionary<string, int>.Empty;
        }

        var result = new Dictionary<string, int>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 3) continue;

            var deviceName = fields[0].Trim();
            var tempStr = fields[^1].Trim();

            if (!int.TryParse(tempStr, out var tempCelsius)) continue;

            // Resolve ZFS device name to physical disk name via the pool-derived mapping.
            // Falls back to short-name resolution for simple device names (e.g. "sda1").
            var physicalDisk = ResolveDeviceName(deviceName, deviceMap);

            if (physicalDisk != null)
            {
                result.TryAdd(physicalDisk, tempCelsius);
            }
            else
            {
                logger.LogWarning("Could not resolve iostat device name: {DeviceName}", deviceName);
            }
        }

        return result.ToFrozenDictionary();
    }

    /// <summary>
    /// Resolves a ZFS device identifier to a physical disk name.
    /// Uses the pool-derived device map for by-id names, and direct
    /// partition stripping for short names like "sda1".
    /// </summary>
    private static string? ResolveDeviceName(string device, IReadOnlyDictionary<string, string> deviceMap)
    {
        // Look up in pool-derived mapping (handles wwn-*, ata-*, scsi-*, etc.)
        if (deviceMap.TryGetValue(device, out var mapped))
            return mapped;

        // Short name fallback: strip partition suffix directly
        var baseName = SystemService.StripPartition(device);
        return SystemService.IsPhysicalDisk(baseName) ? baseName : null;
    }
}

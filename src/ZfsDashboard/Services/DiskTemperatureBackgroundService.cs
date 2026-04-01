using System.Collections.Frozen;
using Zfs.Core;
using Zfs.Core.Services;

namespace ZfsDashboard.Services;

public sealed class DiskTemperatureBackgroundService(
    ICommandExecutor commandExecutor,
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
                var output = await commandExecutor.ExecuteAsync("zpool", "iostat -c temp -H");
                this.temperatures = ParseTemperatures(output);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to collect disk temperatures");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>
    /// Parses the output of <c>zpool iostat -c temp -H</c>.
    /// Each line is tab-separated; the device name is in the second column
    /// and the temperature value is the last column.
    /// </summary>
    internal static IReadOnlyDictionary<string, int> ParseTemperatures(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return FrozenDictionary<string, int>.Empty;

        var result = new Dictionary<string, int>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 3) continue;

            var devicePath = fields[1].Trim();
            var tempStr = fields[^1].Trim();

            if (!int.TryParse(tempStr, out var tempCelsius)) continue;

            // Resolve ZFS device name to physical disk name (e.g. "sda2" → "sda")
            var physicalDisk = ResolveDeviceName(devicePath);
            if (physicalDisk == null) continue;

            // First occurrence wins (in case of duplicate entries)
            result.TryAdd(physicalDisk, tempCelsius);
        }

        return result.ToFrozenDictionary();
    }

    /// <summary>
    /// Resolves a ZFS device identifier to a physical disk name.
    /// Handles both short names (e.g. "sda1") and full paths (e.g. "/dev/disk/by-id/...").
    /// </summary>
    private static string? ResolveDeviceName(string device)
    {
        // If it looks like an absolute path, try symlink resolution
        if (device.StartsWith('/'))
            return SystemService.ResolveToPhysicalDisk(device);

        // Short name: strip partition suffix directly
        var baseName = SystemService.StripPartition(device);
        return SystemService.IsPhysicalDisk(baseName) ? baseName : null;
    }
}

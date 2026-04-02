using System.Collections.Frozen;
using Zfs.Core.Services;

namespace ZfsDashboard.Services;

public sealed class DiskTemperatureBackgroundService(
    ILogger<DiskTemperatureBackgroundService> logger) : BackgroundService, IDiskTemperatureProvider
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);
    private const string HwmonBasePath = "/sys/class/hwmon";

    private volatile IReadOnlyDictionary<string, int> temperatures = FrozenDictionary<string, int>.Empty;

    public IReadOnlyDictionary<string, int> Temperatures => this.temperatures;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run once immediately, then every 60 seconds.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                this.temperatures = ReadTemperatures(HwmonBasePath, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to collect disk temperatures");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>
    /// Reads disk temperatures from sysfs hwmon entries.
    /// Supports "drivetemp" (SATA) and "nvme" hwmon drivers.
    /// </summary>
    internal static IReadOnlyDictionary<string, int> ReadTemperatures(string hwmonBasePath, ILogger logger)
    {
        if (!Directory.Exists(hwmonBasePath))
            return FrozenDictionary<string, int>.Empty;

        var result = new Dictionary<string, int>();

        foreach (var hwmonDir in Directory.GetDirectories(hwmonBasePath))
        {
            var nameFile = Path.Combine(hwmonDir, "name");
            var tempFile = Path.Combine(hwmonDir, "temp1_input");

            if (!File.Exists(nameFile) || !File.Exists(tempFile))
                continue;

            var driverName = File.ReadAllText(nameFile).Trim();
            if (driverName is not ("drivetemp" or "nvme"))
                continue;

            var blockDevice = ResolveBlockDevice(hwmonDir, driverName);
            if (blockDevice is null)
            {
                logger.LogWarning("Could not resolve hwmon to block device: {HwmonDir} ({Driver})", hwmonDir, driverName);
                continue;
            }

            var tempText = File.ReadAllText(tempFile).Trim();
            if (!int.TryParse(tempText, out var millidegrees))
            {
                logger.LogWarning("Could not parse temperature from {TempFile}: {Value}", tempFile, tempText);
                continue;
            }

            result.TryAdd(blockDevice, millidegrees / 1000);
        }

        return result.ToFrozenDictionary();
    }

    /// <summary>
    /// Resolves a hwmon directory to a block device name (e.g. "sda", "nvme0n1").
    /// For drivetemp: follows device symlink → looks for block/ subdirectory.
    /// For nvme: follows device symlink → looks for nvme*n* subdirectory.
    /// </summary>
    internal static string? ResolveBlockDevice(string hwmonDir, string driverName)
    {
        var deviceLink = Path.Combine(hwmonDir, "device");
        if (!Directory.Exists(deviceLink))
            return null;

        var devicePath = Path.GetFullPath(deviceLink);

        if (driverName == "drivetemp")
        {
            // drivetemp device → /sys/devices/.../2:0:0:0/block/sda
            var blockDir = Path.Combine(devicePath, "block");
            if (!Directory.Exists(blockDir))
                return null;

            var entries = Directory.GetDirectories(blockDir);
            return entries.Length > 0 ? Path.GetFileName(entries[0]) : null;
        }

        if (driverName == "nvme")
        {
            // nvme device → /sys/devices/.../nvme/nvme0/nvme0n1
            foreach (var subdir in Directory.GetDirectories(devicePath))
            {
                var name = Path.GetFileName(subdir);
                if (name is not null && name.StartsWith("nvme", StringComparison.Ordinal) && name.Contains('n', StringComparison.Ordinal))
                {
                    // Ensure it's a namespace (nvme0n1), not another nvme sub-path
                    // by checking that the character after the last 'n' is a digit.
                    var lastN = name.LastIndexOf('n');
                    if (lastN > 0 && lastN < name.Length - 1 && char.IsDigit(name[lastN + 1]))
                        return name;
                }
            }
        }

        return null;
    }
}

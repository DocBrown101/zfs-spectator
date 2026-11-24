using System.Globalization;

namespace Zfs.Core.Services;

internal sealed class CpuTemperatureReader(string hwmonBasePath = "/sys/class/hwmon", string thermalBasePath = "/sys/class/thermal")
{
    private readonly Lock sync = new();
    private string? cachedPath;
    private static readonly string[] CpuSensorDriverNames =
        ["coretemp", "k10temp", "zenpower", "cpu_thermal", "soc_thermal"];

    /// <summary>
    /// Reads the current CPU temperature in Celsius. The matching
    /// <c>temp*_input</c> path is discovered lazily and cached; on a missing or
    /// vanished sensor the discovery is retried on the next poll.
    /// </summary>
    internal double? ReadCelsius(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            this.cachedPath ??= FindCpuTemperatureInputPath(hwmonBasePath, thermalBasePath);
            if (this.cachedPath is null) return null;
            try
            {
                if (TryReadCelsius(this.cachedPath) is { } temperature) return temperature;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed sensor read must not discard the other system metrics.
            }
            this.cachedPath = null;
            return null;
        }
    }

    /// <summary>
    /// Discovers the <c>temp*_input</c> file of the best CPU temperature sensor.
    /// Prefers a package/Tctl/Tdie reading, then CPU core sensors, then matching
    /// thermal zones. An unreadable source does not prevent trying the next one.
    /// </summary>
    internal static string? FindCpuTemperatureInputPath(string hwmonBasePath, string thermalBasePath)
        => TryFind(FindHwmonCpuTemperatureInputPath, hwmonBasePath)
           ?? TryFind(FindThermalZoneCpuTemperatureInputPath, thermalBasePath);

    private static string? TryFind(Func<string, string?> find, string basePath)
    {
        try
        {
            return find(basePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? FindHwmonCpuTemperatureInputPath(string hwmonBasePath)
    {
        var inputPaths = Directory.EnumerateDirectories(hwmonBasePath, "hwmon*")
            .Where(dir => SysfsFile.TryReadFirstLine(Path.Combine(dir, "name")) is { } driver &&
                          CpuSensorDriverNames.Contains(driver, StringComparer.OrdinalIgnoreCase))
            .SelectMany(dir => Directory.EnumerateFiles(dir, "temp*_input"));

        string? bestPath = null;
        var bestRank = 0;
        var bestValue = long.MinValue;

        foreach (var inputPath in inputPaths)
        {
            if (TryReadMilliCelsius(inputPath) is not { } value) continue;
            var rank = ClassifySensorLabel(TryReadLabel(inputPath));
            if (rank == 0) continue;
            if (rank > bestRank || (rank == bestRank && value > bestValue))
            {
                bestPath = inputPath;
                bestRank = rank;
                bestValue = value;
            }
        }

        return bestPath;
    }

    private static string? FindThermalZoneCpuTemperatureInputPath(string thermalBasePath)
    {
        string? bestPath = null;
        var bestValue = long.MinValue;

        foreach (var zoneDir in Directory.EnumerateDirectories(thermalBasePath, "thermal_zone*"))
        {
            var type = SysfsFile.TryReadFirstLine(Path.Combine(zoneDir, "type"));
            if (type == null || !IsCpuThermalZoneType(type)) continue;

            var tempPath = Path.Combine(zoneDir, "temp");
            if (TryReadMilliCelsius(tempPath) is not { } value) continue;
            if (value > bestValue)
            {
                bestPath = tempPath;
                bestValue = value;
            }
        }

        return bestPath;
    }

    /// <summary>
    /// Reads a <c>temp*_input</c> value in millidegrees Celsius, or <c>null</c>
    /// when the file is missing, unreadable or contains an implausible value.
    /// </summary>
    private static long? TryReadMilliCelsius(string inputPath)
    {
        var text = SysfsFile.TryReadFirstLine(inputPath);
        if (text == null) return null;

        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milli))
            return null;

        // Plausible range: 0 °C .. 200 °C in millidegrees.
        return milli is > 0 and < 200_000 ? milli : null;
    }

    /// <summary>
    /// Reads a <c>temp*_input</c> file and converts the value to Celsius.
    /// Returns <c>null</c> when unavailable.
    /// </summary>
    internal static double? TryReadCelsius(string inputPath)
    {
        var milli = TryReadMilliCelsius(inputPath);
        return milli == null ? null : milli / 1000.0;
    }

    /// <summary>Ranks a sensor label: 3 = package, 2 = core, 1 = unlabeled CPU sensor, 0 = unrelated.</summary>
    private static int ClassifySensorLabel(string? label)
    {
        if (label == null) return 1;
        if (IsPackageLabel(label)) return 3;
        if (IsCoreLabel(label)) return 2;
        return 0;
    }

    private static bool IsPackageLabel(string label) =>
        label.Contains("package", StringComparison.OrdinalIgnoreCase) ||
        label.Contains("tctl", StringComparison.OrdinalIgnoreCase) ||
        label.Contains("tdie", StringComparison.OrdinalIgnoreCase) ||
        label.Equals("cpu", StringComparison.OrdinalIgnoreCase);

    private static bool IsCoreLabel(string label) =>
        label.Contains("core", StringComparison.OrdinalIgnoreCase) ||
        label.Contains("tccd", StringComparison.OrdinalIgnoreCase);

    private static bool IsCpuThermalZoneType(string type) =>
        type.Contains("cpu", StringComparison.OrdinalIgnoreCase) ||
        type.Contains("package", StringComparison.OrdinalIgnoreCase) ||
        type.Contains("pkg_temp", StringComparison.OrdinalIgnoreCase) ||
        (type.Contains("soc", StringComparison.OrdinalIgnoreCase) &&
         type.Contains("thermal", StringComparison.OrdinalIgnoreCase));

    private static string? TryReadLabel(string inputPath)
    {
        var fileName = Path.GetFileName(inputPath);
        if (!fileName.EndsWith("_input", StringComparison.Ordinal)) return null;

        var labelPath = Path.Combine(
            Path.GetDirectoryName(inputPath) ?? "",
            fileName[..^"_input".Length] + "_label");
        return SysfsFile.TryReadFirstLine(labelPath);
    }
}

namespace Zfs.Tests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZfsDashboard.Services;

public class DiskTemperatureTests : IDisposable
{
    private static readonly ILogger NullLogger = NullLogger<DiskTemperatureBackgroundService>.Instance;
    private readonly string tempDir;

    public DiskTemperatureTests()
    {
        this.tempDir = Path.Combine(Path.GetTempPath(), "hwmon-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(this.tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(this.tempDir))
            Directory.Delete(this.tempDir, recursive: true);
    }

    [Fact]
    public void ReadTemperatures_NoHwmonDir_ReturnsEmpty()
    {
        var result = DiskTemperatureBackgroundService.ReadTemperatures("/nonexistent", NullLogger);
        Assert.Empty(result);
    }

    [Fact]
    public void ReadTemperatures_SkipsNonDiskHwmon()
    {
        CreateHwmonEntry("hwmon0", "coretemp", "45000");

        var result = DiskTemperatureBackgroundService.ReadTemperatures(this.tempDir, NullLogger);

        Assert.Empty(result);
    }

    [Fact]
    public void ReadTemperatures_SkipsEntryWithoutTempFile()
    {
        var hwmon = Path.Combine(this.tempDir, "hwmon0");
        Directory.CreateDirectory(hwmon);
        File.WriteAllText(Path.Combine(hwmon, "name"), "drivetemp");
        // No temp1_input file

        var result = DiskTemperatureBackgroundService.ReadTemperatures(this.tempDir, NullLogger);

        Assert.Empty(result);
    }

    [Fact]
    public void ReadTemperatures_Drivetemp_ReturnsDeviceWithConvertedTemperature()
    {
        var hwmon = CreateHwmonWithDevice("hwmon0", "drivetemp");
        Directory.CreateDirectory(Path.Combine(hwmon, "device", "block", "sda"));
        File.WriteAllText(Path.Combine(hwmon, "temp1_input"), "45000");

        var result = DiskTemperatureBackgroundService.ReadTemperatures(this.tempDir, NullLogger);

        var entry = Assert.Single(result);
        Assert.Equal("sda", entry.Key);
        Assert.Equal(45, entry.Value);
    }

    [Fact]
    public void ReadTemperatures_UnparseableTempValue_IsSkipped()
    {
        var hwmon = CreateHwmonWithDevice("hwmon0", "drivetemp");
        Directory.CreateDirectory(Path.Combine(hwmon, "device", "block", "sda"));
        File.WriteAllText(Path.Combine(hwmon, "temp1_input"), "not-a-number");

        var result = DiskTemperatureBackgroundService.ReadTemperatures(this.tempDir, NullLogger);

        Assert.Empty(result);
    }

    [Fact]
    public void ResolveBlockDevice_Drivetemp_FindsBlockDevice()
    {
        var hwmon = CreateHwmonWithDevice("hwmon0", "drivetemp");
        var blockDir = Path.Combine(hwmon, "device", "block", "sda");
        Directory.CreateDirectory(blockDir);

        var result = DiskTemperatureBackgroundService.ResolveBlockDevice(hwmon, "drivetemp");

        Assert.Equal("sda", result);
    }

    [Fact]
    public void ResolveBlockDevice_Nvme_FindsNamespaceDevice()
    {
        var hwmon = CreateHwmonWithDevice("hwmon0", "nvme");
        var nsDir = Path.Combine(hwmon, "device", "nvme0n1");
        Directory.CreateDirectory(nsDir);

        var result = DiskTemperatureBackgroundService.ResolveBlockDevice(hwmon, "nvme");

        Assert.Equal("nvme0n1", result);
    }

    [Fact]
    public void ResolveBlockDevice_Nvme_IgnoresNonNamespaceSubdirs()
    {
        var hwmon = CreateHwmonWithDevice("hwmon0", "nvme");
        // Create a subdir that starts with "nvme" but isn't a namespace
        Directory.CreateDirectory(Path.Combine(hwmon, "device", "nvme-subsys0"));

        var result = DiskTemperatureBackgroundService.ResolveBlockDevice(hwmon, "nvme");

        Assert.Null(result);
    }

    [Fact]
    public void ResolveBlockDevice_NoDeviceLink_ReturnsNull()
    {
        var hwmon = Path.Combine(this.tempDir, "hwmon0");
        Directory.CreateDirectory(hwmon);

        var result = DiskTemperatureBackgroundService.ResolveBlockDevice(hwmon, "drivetemp");

        Assert.Null(result);
    }

    private string CreateHwmonWithDevice(string name, string driverName)
    {
        var hwmon = Path.Combine(this.tempDir, name);
        var deviceDir = Path.Combine(hwmon, "device");
        Directory.CreateDirectory(deviceDir);
        File.WriteAllText(Path.Combine(hwmon, "name"), driverName);
        return hwmon;
    }

    private void CreateHwmonEntry(string name, string driverName, string tempValue)
    {
        var hwmon = Path.Combine(this.tempDir, name);
        Directory.CreateDirectory(hwmon);
        File.WriteAllText(Path.Combine(hwmon, "name"), driverName);
        File.WriteAllText(Path.Combine(hwmon, "temp1_input"), tempValue);
    }
}

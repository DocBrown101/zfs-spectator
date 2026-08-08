namespace Zfs.Tests;

using Zfs.Core.Models;
using Zfs.Core.Services;
using Zfs.Tests.Helper;

public class SystemServiceTests
{
    [Theory]
    [InlineData("sda2", "sda")]
    [InlineData("sda", "sda")]
    [InlineData("sdb1", "sdb")]
    [InlineData("nvme0n1p1", "nvme0n1")]
    [InlineData("nvme0n1", "nvme0n1")]
    [InlineData("nvme2n1p3", "nvme2n1")]
    [InlineData("vdb1", "vdb")]
    [InlineData("xvda1", "xvda")]
    public void StripPartition_ShouldReturnBaseDiskName(string input, string expected)
    {
        Assert.Equal(expected, SystemService.StripPartition(input));
    }

    // ── IsPhysicalDisk ──────────────────────────────────────────────────

    [Theory]
    [InlineData("sda", true)]
    [InlineData("sdz", true)]
    [InlineData("sdaa", true)]
    [InlineData("sdab", true)]
    [InlineData("sda1", false)]
    [InlineData("sda2", false)]
    [InlineData("nvme0n1", true)]
    [InlineData("nvme0n1p1", false)]
    [InlineData("nvme1n1p3", false)]
    [InlineData("vda", true)]
    [InlineData("vdb", true)]
    [InlineData("vda1", false)]
    [InlineData("xvda", true)]
    [InlineData("xvda1", false)]
    [InlineData("dm-0", false)]
    [InlineData("loop0", false)]
    [InlineData("sr0", false)]
    public void IsPhysicalDisk_ShouldIdentifyWholeDiskDevices(string device, bool expected)
    {
        Assert.Equal(expected, SystemService.IsPhysicalDisk(device));
    }

    // ── ComputeDiskIoRates ──────────────────────────────────────────────

    [Fact]
    public void ComputeDiskIoRates_NoPrevious_ReturnsZeroRates()
    {
        var disks = new List<DiskIoInfo>
        {
            new() { Device = "sda", ReadsCompleted = 100, SectorsRead = 2000, IoTimeMs = 500 },
        };

        var rates = SystemService.ComputeDiskIoRates(disks, null, 0);

        Assert.Single(rates);
        Assert.Equal("sda", rates[0].Device);
        Assert.Equal(0, rates[0].ReadBytesPerSec);
        Assert.Equal(0, rates[0].WriteBytesPerSec);
        Assert.Equal(0, rates[0].UtilizationPct);
    }

    [Fact]
    public void ComputeDiskIoRates_WithPrevious_ComputesDeltas()
    {
        var prev = new List<DiskIoInfo>
        {
            new()
            {
                Device = "sda",
                ReadsCompleted = 100,
                WritesCompleted = 50,
                SectorsRead = 2000,
                SectorsWritten = 1000,
                ReadTimeMs = 200,
                WriteTimeMs = 100,
                IoTimeMs = 500,
            },
        };

        var curr = new List<DiskIoInfo>
        {
            new()
            {
                Device = "sda",
                ReadsCompleted = 200,
                WritesCompleted = 150,
                SectorsRead = 4000,
                SectorsWritten = 3000,
                ReadTimeMs = 700,
                WriteTimeMs = 600,
                IoTimeMs = 1500,
            },
        };

        var rates = SystemService.ComputeDiskIoRates(curr, prev, 1.0);

        Assert.Single(rates);
        var r = rates[0];
        Assert.Equal("sda", r.Device);

        // (4000-2000) * 512 / 1.0 = 1_024_000
        Assert.Equal(1_024_000.0, r.ReadBytesPerSec);
        // (3000-1000) * 512 / 1.0 = 1_024_000
        Assert.Equal(1_024_000.0, r.WriteBytesPerSec);
        // (700-200) / (200-100) = 5.0 ms
        Assert.Equal(5.0, r.ReadLatencyMs);
        // (600-100) / (150-50) = 5.0 ms
        Assert.Equal(5.0, r.WriteLatencyMs);
        // (1500-500) / (1.0*1000) * 100 = 100%
        Assert.Equal(100.0, r.UtilizationPct);
    }

    [Fact]
    public void ComputeDiskIoRates_UtilizationCappedAt100()
    {
        var prev = new List<DiskIoInfo>
        {
            new() { Device = "sda", IoTimeMs = 0 },
        };
        var curr = new List<DiskIoInfo>
        {
            new() { Device = "sda", IoTimeMs = 2000 },
        };

        // 2000ms io_time in 1s wall time = 200%, should cap at 100%
        var rates = SystemService.ComputeDiskIoRates(curr, prev, 1.0);
        Assert.Equal(100.0, rates[0].UtilizationPct);
    }

    [Fact]
    public void ComputeDiskIoRates_CounterWrap_ReturnsZeroForWrappedFields()
    {
        var prev = new List<DiskIoInfo>
        {
            new() { Device = "sda", ReadsCompleted = 100, SectorsRead = 5000, IoTimeMs = 1000 },
        };
        var curr = new List<DiskIoInfo>
        {
            // Simulated counter wrap: current < previous
            new() { Device = "sda", ReadsCompleted = 50, SectorsRead = 2000, IoTimeMs = 500 },
        };

        var rates = SystemService.ComputeDiskIoRates(curr, prev, 1.0);
        Assert.Equal(0, rates[0].ReadBytesPerSec);
        Assert.Equal(0, rates[0].UtilizationPct);
    }

    [Fact]
    public void ComputeDiskIoRates_EmptyList_ReturnsEmpty()
    {
        var rates = SystemService.ComputeDiskIoRates([], null, 0);
        Assert.Empty(rates);
    }

    [Fact]
    public void ComputeDiskIoRates_NewDiskNotInPrevious_ReturnsZeroRates()
    {
        var prev = new List<DiskIoInfo>
        {
            new() { Device = "sda", ReadsCompleted = 100 },
        };
        var curr = new List<DiskIoInfo>
        {
            new() { Device = "sdb", ReadsCompleted = 50 },
        };

        var rates = SystemService.ComputeDiskIoRates(curr, prev, 1.0);
        Assert.Single(rates);
        Assert.Equal("sdb", rates[0].Device);
        Assert.Equal(0, rates[0].ReadBytesPerSec);
    }

    // ── GetDashboardDataAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardDataAsync_PopulatesPoolScrubsFromSnapshots()
    {
        var pools = new List<Pool> { TestDataHelpers.MakePool("tank"), TestDataHelpers.MakePool("backup") };
        var scrubs = new Dictionary<string, ScrubInfo>
        {
            ["tank"] = new() { State = "finished", Errors = 0, FinishTime = "2026-03-30" },
            ["backup"] = ScrubInfo.Idle,
        };
        var snapshots = pools.Select(pool => (pool, scrubs.GetValueOrDefault(pool.Name, ScrubInfo.Idle))).ToList();

        var data = await new SystemService().GetDashboardDataAsync(new TestDataHelpers.StubZfsService(), snapshots);

        Assert.Equal(2, data.PoolScrubs.Count);
        Assert.Equal("finished", data.PoolScrubs["tank"].State);
        Assert.Equal("idle", data.PoolScrubs["backup"].State);
    }

    [Fact]
    public async Task GetDashboardDataAsync_NoPools_ReturnsEmptyPoolScrubs()
    {
        var data = await new SystemService().GetDashboardDataAsync(new TestDataHelpers.StubZfsService(), []);

        Assert.Empty(data.PoolScrubs);
    }

    // ── RunSafeAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task RunSafeAsync_Exception_ReturnsFallback()
    {
        var result = await SystemService.RunSafeAsync(
            async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("boom");
            },
            42);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task RunSafeAsync_NoException_ReturnsResult()
    {
        var result = await SystemService.RunSafeAsync(() => Task.FromResult(7), 0);

        Assert.Equal(7, result);
    }

    [Fact]
    public async Task RunSafeAsync_Cancellation_IsPropagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SystemService.RunSafeAsync(() => Task.FromCanceled<int>(cts.Token), 0));
    }

    // ── CPU topology ────────────────────────────────────────────────────

    private static List<string> CpuInfoBlock(int processor, int physicalId, int coreId)
        => [$"processor\t: {processor}", $"physical id\t: {physicalId}", $"core id\t: {coreId}"];

    [Fact]
    public void CountCores_SmtSystem_ReportsPhysicalAndLogicalCores()
    {
        var lines = new List<string>();
        var processor = 0;
        for (var thread = 0; thread < 2; thread++)
        {
            for (var core = 0; core < 8; core++)
                lines.AddRange(CpuInfoBlock(processor++, 0, core));
        }

        Assert.Equal(8, SystemService.CountPhysicalCoresFromCpuInfo(lines));
        Assert.Equal(16, SystemService.CountLogicalCores(lines));
    }

    [Fact]
    public void CountPhysicalCores_MultiSocket_CountsUniquePairsAcrossSockets()
    {
        var lines = new List<string>();
        var processor = 0;
        for (var socket = 0; socket < 2; socket++)
        {
            for (var thread = 0; thread < 2; thread++)
            {
                for (var core = 0; core < 4; core++)
                    lines.AddRange(CpuInfoBlock(processor++, socket, core));
            }
        }

        Assert.Equal(8, SystemService.CountPhysicalCoresFromCpuInfo(lines));
        Assert.Equal(16, SystemService.CountLogicalCores(lines));
    }

    [Fact]
    public void CountPhysicalCores_MissingTopologyInfo_ReturnsNullWhileLogicalStillCounts()
    {
        var lines = Enumerable.Range(0, 16).Select(i => $"processor\t: {i}").ToList();

        Assert.Null(SystemService.CountPhysicalCoresFromCpuInfo(lines));
        Assert.Equal(16, SystemService.CountLogicalCores(lines));
    }

    [Fact]
    public void CountPhysicalCores_IncompleteBlock_ReturnsNull()
    {
        var lines = new List<string>
        {
            "processor\t: 0",
            "physical id\t: 0",
            "core id\t: 0",
            "processor\t: 1",
            "physical id\t: 0",
        };

        Assert.Null(SystemService.CountPhysicalCoresFromCpuInfo(lines));
    }

    [Fact]
    public void CountPhysicalCoresFromSysfs_CountsUniquePackageCorePairs()
    {
        using var root = TempDirectory.Create();
        // cpu0/cpu1 share (0, 0) → SMT; cpu2 = (0, 1); cpu3 = (1, 0)
        WriteCpuTopology(root.RootPath, "cpu0", "0", "0");
        WriteCpuTopology(root.RootPath, "cpu1", "0", "0");
        WriteCpuTopology(root.RootPath, "cpu2", "0", "1");
        WriteCpuTopology(root.RootPath, "cpu3", "1", "0");

        Assert.Equal(3, SystemService.CountPhysicalCoresFromSysfs(root.RootPath));
    }

    [Fact]
    public void CountPhysicalCoresFromSysfs_MissingTopologyFiles_ReturnsNull()
    {
        using var root = TempDirectory.Create();
        Directory.CreateDirectory(Path.Combine(root.RootPath, "cpu0"));
        Directory.CreateDirectory(Path.Combine(root.RootPath, "cpufreq"));

        Assert.Null(SystemService.CountPhysicalCoresFromSysfs(root.RootPath));
    }

    [Fact]
    public void CountPhysicalCoresFromSysfs_MissingBasePath_ReturnsNull()
    {
        Assert.Null(SystemService.CountPhysicalCoresFromSysfs(
            Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid())));
    }

    // ── CPU temperature ─────────────────────────────────────────────────

    [Fact]
    public void TryReadCelsius_ConvertsMilliDegreesToCelsius()
    {
        using var root = TempDirectory.Create();
        var inputPath = Path.Combine(root.RootPath, "temp1_input");
        File.WriteAllText(inputPath, "52437");

        var celsius = SystemService.TryReadCelsius(inputPath);
        Assert.NotNull(celsius);
        Assert.Equal(52.437, celsius.Value, 3);
    }

    [Fact]
    public void FindCpuTemperatureInputPath_PrefersPackageOverCoreSensor()
    {
        using var root = TempDirectory.Create();
        var hwmon = Directory.CreateDirectory(Path.Combine(root.RootPath, "hwmon")).FullName;
        var coretemp = Directory.CreateDirectory(Path.Combine(hwmon, "hwmon0")).FullName;
        File.WriteAllText(Path.Combine(coretemp, "name"), "coretemp\n");

        var coreInput = Path.Combine(coretemp, "temp1_input");
        var packageInput = Path.Combine(coretemp, "temp2_input");
        File.WriteAllText(Path.Combine(coretemp, "temp1_label"), "Core 0\n");
        File.WriteAllText(coreInput, "60000\n");
        File.WriteAllText(Path.Combine(coretemp, "temp2_label"), "Package id 0\n");
        File.WriteAllText(packageInput, "50000\n");
        var thermal = Directory.CreateDirectory(Path.Combine(root.RootPath, "thermal")).FullName;

        var chosen = SystemService.FindCpuTemperatureInputPath(hwmon, thermal);

        Assert.NotNull(chosen);
        Assert.Equal(packageInput, chosen);
        Assert.Equal(50.0, SystemService.TryReadCelsius(chosen));
    }

    [Fact]
    public void FindCpuTemperatureInputPath_MultiplePackages_ChoosesHighestValue()
    {
        using var root = TempDirectory.Create();
        var hwmon = Directory.CreateDirectory(Path.Combine(root.RootPath, "hwmon")).FullName;
        var coretemp = Directory.CreateDirectory(Path.Combine(hwmon, "hwmon0")).FullName;
        File.WriteAllText(Path.Combine(coretemp, "name"), "coretemp\n");

        var package0 = Path.Combine(coretemp, "temp1_input");
        var package1 = Path.Combine(coretemp, "temp2_input");
        File.WriteAllText(Path.Combine(coretemp, "temp1_label"), "Package id 0\n");
        File.WriteAllText(package0, "45000\n");
        File.WriteAllText(Path.Combine(coretemp, "temp2_label"), "Package id 1\n");
        File.WriteAllText(package1, "52000\n");
        var thermal = Directory.CreateDirectory(Path.Combine(root.RootPath, "thermal")).FullName;

        var chosen = SystemService.FindCpuTemperatureInputPath(hwmon, thermal);

        Assert.NotNull(chosen);
        Assert.Equal(package1, chosen);
        Assert.Equal(52.0, SystemService.TryReadCelsius(chosen));
    }

    [Fact]
    public void FindCpuTemperatureInputPath_FallsBackToHighestCoreSensorWithoutPackage()
    {
        using var root = TempDirectory.Create();
        var hwmon = Directory.CreateDirectory(Path.Combine(root.RootPath, "hwmon")).FullName;
        var coretemp = Directory.CreateDirectory(Path.Combine(hwmon, "hwmon0")).FullName;
        File.WriteAllText(Path.Combine(coretemp, "name"), "coretemp\n");

        var core0 = Path.Combine(coretemp, "temp1_input");
        var core1 = Path.Combine(coretemp, "temp2_input");
        File.WriteAllText(Path.Combine(coretemp, "temp1_label"), "Core 0\n");
        File.WriteAllText(core0, "45000\n");
        File.WriteAllText(Path.Combine(coretemp, "temp2_label"), "Core 1\n");
        File.WriteAllText(core1, "52000\n");
        var thermal = Directory.CreateDirectory(Path.Combine(root.RootPath, "thermal")).FullName;

        var chosen = SystemService.FindCpuTemperatureInputPath(hwmon, thermal);

        Assert.NotNull(chosen);
        Assert.Equal(core1, chosen);
        Assert.Equal(52.0, SystemService.TryReadCelsius(chosen));
    }

    [Fact]
    public void FindCpuTemperatureInputPath_IgnoresNonCpuSensors()
    {
        using var root = TempDirectory.Create();
        var hwmon = Directory.CreateDirectory(Path.Combine(root.RootPath, "hwmon")).FullName;
        foreach (var (driver, label, value) in new[]
        {
            ("nvme", "Composite", "90000"),
            ("amdgpu", "edge", "110000"),
        })
        {
            var dir = Directory.CreateDirectory(Path.Combine(hwmon, "hwmon" + driver.Length)).FullName;
            File.WriteAllText(Path.Combine(dir, "name"), driver + "\n");
            File.WriteAllText(Path.Combine(dir, "temp1_label"), label + "\n");
            File.WriteAllText(Path.Combine(dir, "temp1_input"), value + "\n");
        }

        var thermal = Directory.CreateDirectory(Path.Combine(root.RootPath, "thermal")).FullName;

        Assert.Null(SystemService.FindCpuTemperatureInputPath(hwmon, thermal));
    }

    [Fact]
    public void FindCpuTemperatureInputPath_ThermalZoneFallback_Works()
    {
        using var root = TempDirectory.Create();
        var hwmon = Directory.CreateDirectory(Path.Combine(root.RootPath, "hwmon")).FullName;
        var thermal = Directory.CreateDirectory(Path.Combine(root.RootPath, "thermal")).FullName;

        var cpuZone = Directory.CreateDirectory(Path.Combine(thermal, "thermal_zone0")).FullName;
        File.WriteAllText(Path.Combine(cpuZone, "type"), "x86_pkg_temp\n");
        var cpuZoneTemp = Path.Combine(cpuZone, "temp");
        File.WriteAllText(cpuZoneTemp, "52437\n");

        // Non-CPU thermal zone must not win, even with a higher value.
        var acpiZone = Directory.CreateDirectory(Path.Combine(thermal, "thermal_zone1")).FullName;
        File.WriteAllText(Path.Combine(acpiZone, "type"), "acpitz\n");
        File.WriteAllText(Path.Combine(acpiZone, "temp"), "99999\n");

        var chosen = SystemService.FindCpuTemperatureInputPath(hwmon, thermal);

        Assert.NotNull(chosen);
        Assert.Equal(cpuZoneTemp, chosen);
        var celsius = SystemService.TryReadCelsius(chosen);
        Assert.NotNull(celsius);
        Assert.Equal(52.437, celsius.Value, 3);
    }

    [Fact]
    public void FindCpuTemperatureInputPath_MissingDirectories_ReturnsNull()
    {
        var missing = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid());

        Assert.Null(SystemService.FindCpuTemperatureInputPath(missing, missing));
    }

    [Fact]
    public void FindCpuTemperatureInputPath_InvalidSensorValues_ReturnsNull()
    {
        using var root = TempDirectory.Create();
        var hwmon = Directory.CreateDirectory(Path.Combine(root.RootPath, "hwmon")).FullName;
        var coretemp = Directory.CreateDirectory(Path.Combine(hwmon, "hwmon0")).FullName;
        File.WriteAllText(Path.Combine(coretemp, "name"), "coretemp\n");
        File.WriteAllText(Path.Combine(coretemp, "temp1_label"), "Package id 0\n");
        File.WriteAllText(Path.Combine(coretemp, "temp1_input"), "garbage\n");
        var thermal = Directory.CreateDirectory(Path.Combine(root.RootPath, "thermal")).FullName;

        Assert.Null(SystemService.FindCpuTemperatureInputPath(hwmon, thermal));
    }

    [Fact]
    public void TryReadCelsius_MissingOrImplausibleFile_ReturnsNull()
    {
        using var root = TempDirectory.Create();
        var inputPath = Path.Combine(root.RootPath, "temp1_input");

        Assert.Null(SystemService.TryReadCelsius(inputPath));

        File.WriteAllText(inputPath, "not-a-number\n");
        Assert.Null(SystemService.TryReadCelsius(inputPath));

        File.WriteAllText(inputPath, "0\n");
        Assert.Null(SystemService.TryReadCelsius(inputPath));
    }

    private static void WriteCpuTopology(string cpuBasePath, string cpuName, string package, string coreId)
    {
        var topologyDir = Directory.CreateDirectory(Path.Combine(cpuBasePath, cpuName, "topology"));
        File.WriteAllText(Path.Combine(topologyDir.FullName, "physical_package_id"), package + "\n");
        File.WriteAllText(Path.Combine(topologyDir.FullName, "core_id"), coreId + "\n");
    }

    private sealed class TempDirectory : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(), "zfs-spectator-tests-" + Guid.NewGuid().ToString("N"));

        private TempDirectory() => Directory.CreateDirectory(this.RootPath);

        public static TempDirectory Create() => new();

        public void Dispose()
        {
            try
            {
                Directory.Delete(this.RootPath, recursive: true);
            }
            catch (Exception)
            {
                // Best effort cleanup; the temp dir is not critical.
            }
        }
    }
}

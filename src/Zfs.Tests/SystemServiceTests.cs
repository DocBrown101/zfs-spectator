namespace Zfs.Tests;

using Zfs.Core.Models;
using Zfs.Core.Services;

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
        // (200-100) / 1.0 = 100
        Assert.Equal(100.0, r.ReadOpsPerSec);
        Assert.Equal(100.0, r.WriteOpsPerSec);
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
        Assert.Equal(0, rates[0].ReadOpsPerSec);
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
}

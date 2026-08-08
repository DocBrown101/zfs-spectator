using Zfs.Core.Models;
using Zfs.Tests.Helper;
using ZfsDashboard.Services;
using ZfsDashboard.ViewModels.Dashboard;

namespace Zfs.Tests;

public class DashboardViewModelTests
{
    [Fact]
    public void LiveResponse_KeepsOptionalArcRowsInTheStableContract()
    {
        var response = new DashboardLiveViewModel(new DashboardSnapshot(
            new DashboardData
            {
                System = new SystemInfo
                {
                    Arc = new ArcStats { MaxSize = 1024 },
                },
            },
            [],
            new StaticSystemInfo()));

        Assert.Equal(["arcSize", "arcHitRate", "arcMeta", "arcData", "arcMruMfu"], response.Arc.Details.Select(row => row.ElementId));
        Assert.True(response.Arc.IsVisible);
        Assert.True(response.Arc.Details[0].IsVisible);
        Assert.True(response.Arc.Details.Single(row => row.ElementId == "arcHitRate").IsVisible);
        Assert.All(response.Arc.Details.Where(row => row.ElementId is "arcMeta" or "arcData" or "arcMruMfu"), row => Assert.False(row.IsVisible));
    }

    [Fact]
    public void LiveResponse_KeepsUnavailableArcInTheStableContract()
    {
        var response = new DashboardLiveViewModel(new DashboardSnapshot(
            new DashboardData(), [], new StaticSystemInfo()));

        Assert.False(response.Arc.IsVisible);
        Assert.Null(response.Arc.L2HitRate);
        Assert.Equal(5, response.Arc.Details.Count);
    }

    [Fact]
    public void CpuCard_FormatsUnavailableCoreCountsAsUnknown()
    {
        var response = new CpuCardViewModel(new SystemInfo(), new StaticSystemInfo());

        Assert.Equal("unknown / unknown", response.Details.Single(row => row.Label == "Cores (physical / logical)").Value);
        Assert.Equal("N/A", response.Details.Single(row => row.Label == "Temperature").Value);
    }

    [Fact]
    public void CpuCard_FormatsCoreCountsAndTemperature()
    {
        var response = new CpuCardViewModel(
            new SystemInfo { CpuTemperatureCelsius = 52.437 },
            new StaticSystemInfo { Processor = "AMD Ryzen 7", PhysicalCoreCount = 8, LogicalCoreCount = 16 });

        Assert.Equal("AMD Ryzen 7", response.Details.Single(row => row.Label == "Processor").Value);
        Assert.Equal("8 / 16", response.Details.Single(row => row.Label == "Cores (physical / logical)").Value);
        Assert.Equal("52.4 °C", response.Details.Single(row => row.Label == "Temperature").Value);
        Assert.Equal("cpuTemperature", response.Details.Single(row => row.Label == "Temperature").ElementId);
        Assert.Equal("", response.Details.Single(row => row.Label == "Temperature").ValueCss);
    }

    [Theory]
    [InlineData(52.4, "")]
    [InlineData(80.0, "text-warning")]
    [InlineData(95.0, "text-danger")]
    [InlineData(null, "")]
    public void CpuCard_AppliesTemperatureCssForThresholds(double? temperature, string expected)
    {
        var response = new CpuCardViewModel(
            new SystemInfo { CpuTemperatureCelsius = temperature },
            new StaticSystemInfo());

        Assert.Equal(expected, response.Details.Single(row => row.Label == "Temperature").ValueCss);
    }

    [Fact]
    public void CpuCard_FormatsPartialUnknownCores()
    {
        var response = new CpuCardViewModel(
            new SystemInfo(),
            new StaticSystemInfo { PhysicalCoreCount = -1, LogicalCoreCount = 16 });

        Assert.Equal("unknown / 16", response.Details.Single(row => row.Label == "Cores (physical / logical)").Value);
    }

    [Fact]
    public void LiveResponse_ExposesCpuTemperatureForLiveUpdates()
    {
        var response = new DashboardLiveViewModel(new DashboardSnapshot(
            new DashboardData
            {
                System = new SystemInfo { CpuTemperatureCelsius = 52.437 },
            },
            [],
            new StaticSystemInfo()));

        Assert.Equal(52.437, response.CpuTemperatureCelsius);
        Assert.Equal("", response.CpuTemperatureCss);
    }

    [Theory]
    [InlineData(52.4, "")]
    [InlineData(80.0, "text-warning")]
    [InlineData(95.0, "text-danger")]
    [InlineData(null, "")]
    public void LiveResponse_ExposesCpuTemperatureCssFromBackend(double? temperature, string expected)
    {
        var response = new DashboardLiveViewModel(new DashboardSnapshot(
            new DashboardData
            {
                System = new SystemInfo { CpuTemperatureCelsius = temperature },
            },
            [],
            new StaticSystemInfo()));

        Assert.Equal(expected, response.CpuTemperatureCss);
    }

    [Fact]
    public void LiveResponse_ComputesNetworkRateTotals()
    {
        var response = new DashboardLiveViewModel(new DashboardSnapshot(
            new DashboardData
            {
                NetworkRates =
                [
                    new() { Name = "eth0", RxBytesPerSec = 100, TxBytesPerSec = 50 },
                    new() { Name = "eth1", RxBytesPerSec = 40.5, TxBytesPerSec = 20 },
                ],
            },
            [],
            new StaticSystemInfo()));

        Assert.Equal(140.5, response.NetworkDownloadBytesPerSecond);
        Assert.Equal(70, response.NetworkUploadBytesPerSecond);
    }

    [Fact]
    public void LiveResponse_ComputesNetworkRateTotalsWithoutRates()
    {
        var response = new DashboardLiveViewModel(new DashboardSnapshot(
            new DashboardData(),
            [],
            new StaticSystemInfo()));

        Assert.Equal(0, response.NetworkDownloadBytesPerSecond);
        Assert.Equal(0, response.NetworkUploadBytesPerSecond);
    }

    [Fact]
    public void LiveResponse_ComputesDiskIoRateTotals()
    {
        var response = new DashboardLiveViewModel(new DashboardSnapshot(
            new DashboardData
            {
                DiskIoRates =
                [
                    new() { Device = "sda", ReadBytesPerSec = 200, WriteBytesPerSec = 100 },
                    new() { Device = "sdb", ReadBytesPerSec = 60.5, WriteBytesPerSec = 30 },
                ],
            },
            [],
            new StaticSystemInfo()));

        Assert.Equal(260.5, response.DiskReadBytesPerSecond);
        Assert.Equal(130, response.DiskWriteBytesPerSecond);
    }

    [Fact]
    public void LiveResponse_ExposesPoolCount()
    {
        var response = new DashboardLiveViewModel(new DashboardSnapshot(
            new DashboardData(),
            [TestDataHelpers.MakePool("tank")],
            new StaticSystemInfo()));

        Assert.Equal(1, response.PoolCount);
        Assert.Single(response.Pools);
    }

    [Fact]
    public void LiveResponse_FormatsCpuTemperatureForLiveUpdates()
    {
        var response = new DashboardLiveViewModel(new DashboardSnapshot(
            new DashboardData
            {
                System = new SystemInfo { CpuTemperatureCelsius = 52.437 },
            },
            [],
            new StaticSystemInfo()));

        Assert.Equal("52.4 °C", response.CpuTemperatureText);
    }

    [Fact]
    public void LiveResponse_FormatsUnavailableCpuTemperatureAsNA()
    {
        var response = new DashboardLiveViewModel(new DashboardSnapshot(
            new DashboardData(),
            [],
            new StaticSystemInfo()));

        Assert.Equal("N/A", response.CpuTemperatureText);
    }

    [Fact]
    public void PoolCard_FormatsUsagePercentText()
    {
        var card = new PoolCardViewModel(TestDataHelpers.MakePool("tank") with { UsableSize = 1000, UsableUsed = 524 });

        Assert.Equal(52.4, card.UsagePercent, 1);
        Assert.Equal("52%", card.UsagePercentText);
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(71, 71)]
    public void PoolCard_ClampsUsagePercentWithinRange(ulong used, double expected)
    {
        var card = new PoolCardViewModel(TestDataHelpers.MakePool("tank") with { UsableSize = 100, UsableUsed = used });

        Assert.Equal(expected, card.ClampedUsagePercent);
    }

    [Fact]
    public void PoolCard_ClampsUsagePercentAboveHundred()
    {
        var card = new PoolCardViewModel(TestDataHelpers.MakePool("tank") with { UsableSize = 100, UsableUsed = 150 });

        Assert.Equal(100, card.ClampedUsagePercent);
        Assert.Equal(150, card.UsagePercent);
    }

    [Fact]
    public void DiskIoRate_FormatsUtilizationAndTemperatureText()
    {
        var rate = new DiskIoRateViewModel(new DiskIoRateInfo { UtilizationPct = 55.437, Temperature = 42 });

        Assert.Equal(55.437, rate.UtilizationPercent);
        Assert.Equal("55.4%", rate.UtilizationPercentText);
        Assert.Equal("42 °C", rate.TemperatureText);
    }

    [Fact]
    public void DiskIoRate_FormatsMissingTemperatureAsDash()
    {
        var rate = new DiskIoRateViewModel(new DiskIoRateInfo());

        Assert.Equal("\u2013", rate.TemperatureText);
    }

    [Fact]
    public void ArcCard_FormatsHitRateText()
    {
        var response = new DashboardLiveViewModel(new DashboardSnapshot(
            new DashboardData
            {
                System = new SystemInfo
                {
                    Arc = new ArcStats { Hits = 900, Misses = 100, MaxSize = 1024 },
                },
            },
            [],
            new StaticSystemInfo()));

        Assert.Equal("90.0%", response.Arc.HitRateText);
        Assert.Null(response.Arc.L2HitRateText);
    }

    [Fact]
    public void ArcCard_FormatsL2HitRateTextIncludingSize()
    {
        var response = new DashboardLiveViewModel(new DashboardSnapshot(
            new DashboardData
            {
                System = new SystemInfo
                {
                    Arc = new ArcStats { L2Hits = 700, L2Misses = 300, L2Size = 4096, MaxSize = 1024 },
                },
            },
            [],
            new StaticSystemInfo()));

        Assert.Equal("70.0% (4 KiB)", response.Arc.L2HitRateText);
    }

    [Fact]
    public void PoolCard_ExposesCapacityCssForLiveUpdates()
    {
        var pool = TestDataHelpers.MakePool("tank") with { UsableSize = 100, UsableUsed = 71 };

        var card = new PoolCardViewModel(pool);

        Assert.Equal("bg-warning", card.CapacityCss);
    }
}

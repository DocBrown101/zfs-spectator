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
    public void PoolCard_ExposesCapacityCssForLiveUpdates()
    {
        var pool = TestDataHelpers.MakePool("tank") with { UsableSize = 100, UsableUsed = 71 };

        var card = new PoolCardViewModel(pool);

        Assert.Equal("bg-warning", card.CapacityCss);
    }
}

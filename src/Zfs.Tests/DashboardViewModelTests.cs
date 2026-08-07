using Zfs.Core.Models;
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
    public void CpuCard_FormatsUnavailableCpuCountAsUnknown()
    {
        var response = new CpuCardViewModel(new SystemInfo(), new StaticSystemInfo());

        Assert.Equal("unknown", response.Details.Single(row => row.Label == "CPU Count").Value);
    }
}

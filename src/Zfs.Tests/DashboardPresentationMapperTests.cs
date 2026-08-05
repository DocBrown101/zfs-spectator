using Zfs.Core.Models;
using ZfsDashboard.Presentation;

namespace Zfs.Tests;

public class DashboardPresentationMapperTests
{
    [Fact]
    public void MapPageAndLive_UseTheSameMemoryAndArcModels()
    {
        var system = new SystemInfo
        {
            Uptime = "1h 2m 3s",
            CpuUsagePercent = 12.5,
            Memory = new MemoryInfo
            {
                Total = 4096,
                Available = 1024,
                Used = 3072,
                Buffers = 256,
                Cached = 512,
                SwapTotal = 2048,
                SwapUsed = 1024,
            },
            Arc = new ArcStats
            {
                Size = 1024,
                MaxSize = 2048,
                MetadataSize = 128,
                DataSize = 896,
                MruSize = 256,
                MfuSize = 640,
                Hits = 90,
                Misses = 10,
            },
        };

        var page = DashboardPresentationMapper.MapPage([], system, new StaticSystemInfo());
        var live = DashboardPresentationMapper.MapLive(new DashboardData { System = system });

        Assert.Equal(page.Memory.UsagePercent, live.Memory.UsagePercent);
        Assert.Equal(page.Memory.Details, live.Memory.Details);
        Assert.Equal(page.Arc.UsagePercent, live.Arc.UsagePercent);
        Assert.Equal(page.Arc.HitRate, live.Arc.HitRate);
        Assert.Equal(page.Arc.Details, live.Arc.Details);
    }

    [Fact]
    public void MapLive_KeepsOptionalArcRowsInTheStableContract()
    {
        var response = DashboardPresentationMapper.MapLive(new DashboardData
        {
            System = new SystemInfo
            {
                Arc = new ArcStats { MaxSize = 1024 },
            },
        });

        Assert.Equal(["arcSize", "arcMeta", "arcData", "arcMruMfu"], response.Arc.Details.Select(row => row.ElementId));
        Assert.True(response.Arc.IsVisible);
        Assert.True(response.Arc.Details[0].IsVisible);
        Assert.All(response.Arc.Details.Skip(1), row => Assert.False(row.IsVisible));
    }

    [Fact]
    public void MapLive_KeepsUnavailableArcInTheStableContract()
    {
        var response = DashboardPresentationMapper.MapLive(new DashboardData());

        Assert.False(response.Arc.IsVisible);
        Assert.Null(response.Arc.L2HitRate);
        Assert.Equal(4, response.Arc.Details.Count);
    }
}

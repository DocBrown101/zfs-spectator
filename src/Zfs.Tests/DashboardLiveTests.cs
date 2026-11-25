using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Tests.Helper;
using ZfsDashboard.Pages;
using ZfsDashboard.Services;
using ZfsDashboard.ViewModels.Dashboard;

namespace Zfs.Tests;

public class DashboardLiveTests
{
    [Fact]
    public void OnGetLive_WithoutSnapshot_ReturnsServiceUnavailableAndRetryHeaders()
    {
        var page = CreatePage(null);

        var result = Assert.IsType<StatusCodeResult>(page.OnGetLive());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("1", page.Response.Headers.RetryAfter.ToString());
        Assert.Equal("no-store", page.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public void OnGetLive_WithSnapshot_ReturnsJsonWithFrontendPropertyNames()
    {
        var page = CreatePage(new DashboardSnapshot(
            new DashboardData
            {
                System = new SystemInfo
                {
                    Uptime = "1h 2m 3s",
                    CpuUsagePercent = 5,
                    Memory = new MemoryInfo { Total = 1024, Used = 512, Available = 512 },
                },
                DiskIoRates =
                [
                    new DiskIoRateInfo
                    {
                        Device = "sda",
                        ReadBytesPerSec = 1024,
                        WriteBytesPerSec = 512,
                        ReadLatencyMs = 1.5,
                        WriteLatencyMs = 2.0,
                        QueueDepth = 0.5,
                        UtilizationPct = 25.0,
                    },
                ],
            },
            [],
            new StaticSystemInfo()));

        var result = Assert.IsType<JsonResult>(page.OnGetLive());
        Assert.Equal("no-store", page.Response.Headers.CacheControl.ToString());
        Assert.False(page.Response.Headers.ContainsKey("Retry-After"));
        var response = Assert.IsType<DashboardLiveViewModel>(result.Value);
        using var doc = Serialize(response);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("diskIoRates", out var rates));
        Assert.True(root.TryGetProperty("networkRates", out _));
        Assert.Equal(5, root.GetProperty("cpuUsagePercent").GetDouble());
        Assert.True(root.TryGetProperty("memory", out _));
        Assert.True(root.TryGetProperty("arc", out _));
        Assert.True(root.TryGetProperty("pools", out _));

        var dev = rates[0];
        Assert.Equal("sda", dev.GetProperty("device").GetString());
        Assert.True(dev.TryGetProperty("readBytesPerSecond", out _));
        Assert.True(dev.TryGetProperty("writeBytesPerSecond", out _));
        Assert.True(dev.TryGetProperty("readRate", out _));
        Assert.True(dev.TryGetProperty("writeRate", out _));
        Assert.True(dev.TryGetProperty("queueDepth", out _));
        Assert.True(dev.TryGetProperty("utilizationPercent", out _));
    }

    [Fact]
    public void DashboardLiveViewModel_ShouldSerializePoolSummaryContract()
    {
        var pool = TestDataHelpers.MakePool("tank") with { UsableSize = 100, UsableUsed = 25, UsableAvail = 75 };
        var response = new DashboardLiveViewModel(new DashboardSnapshot(new DashboardData(), [pool], new StaticSystemInfo()));

        using var doc = Serialize(response);
        var summary = doc.RootElement.GetProperty("pools")[0].GetProperty("summary");

        Assert.True(summary.TryGetProperty("size", out _));
        Assert.True(summary.TryGetProperty("allocated", out _));
        Assert.True(summary.TryGetProperty("free", out _));
        Assert.True(summary.TryGetProperty("usagePercent", out _));
        Assert.True(summary.TryGetProperty("health", out _));
        Assert.True(summary.TryGetProperty("healthCss", out _));
        Assert.True(summary.TryGetProperty("encrypted", out _));
        Assert.True(summary.TryGetProperty("encryptionAlgorithm", out _));
        Assert.True(summary.TryGetProperty("hasErrors", out _));
        Assert.True(summary.TryGetProperty("errorTooltip", out _));
    }

    private static JsonDocument Serialize(DashboardLiveViewModel response) =>
        JsonSerializer.SerializeToDocument(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static IndexModel CreatePage(DashboardSnapshot? snapshot) =>
        new(new StubSnapshotProvider(snapshot))
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
        };

    private sealed class StubSnapshotProvider(DashboardSnapshot? snapshot) : IDashboardSnapshotProvider
    {
        public DashboardSnapshot? Current { get; } = snapshot;
        public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<StaticSystemInfo> GetStaticSystemInfoAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

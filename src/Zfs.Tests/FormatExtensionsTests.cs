namespace Zfs.Tests;

using ZfsDashboard.Presentation;

public class FormatExtensionsTests
{
    [Theory]
    [InlineData("ONLINE", "text-bg-success")]
    [InlineData("DEGRADED", "text-bg-warning")]
    [InlineData("FAULTED", "text-bg-danger")]
    [InlineData("UNAVAIL", "text-bg-danger")]
    [InlineData("OFFLINE", "text-bg-secondary")]
    [InlineData("REMOVED", "text-bg-secondary")]
    [InlineData("UNKNOWN", "text-bg-secondary")]
    public void ToStatusBadgeCss_MapsStatusToBootstrapBadge(string status, string expected)
    {
        Assert.Equal(expected, status.ToStatusBadgeCss());
    }

    [Theory]
    [InlineData(0, "bg-success")]
    [InlineData(70, "bg-success")]
    [InlineData(70.0001, "bg-warning")]
    [InlineData(85, "bg-warning")]
    [InlineData(85.0001, "bg-danger")]
    [InlineData(100, "bg-danger")]
    public void ToCapacityCss_UsesTheSharedCapacityThresholds(double percentage, string expected)
    {
        Assert.Equal(expected, percentage.ToCapacityCss());
    }
}

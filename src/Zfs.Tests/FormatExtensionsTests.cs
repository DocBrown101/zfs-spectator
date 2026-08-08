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
}

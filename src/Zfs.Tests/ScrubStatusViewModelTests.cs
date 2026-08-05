using Zfs.Core.Models;
using ZfsDashboard.ViewModels.Shared;

namespace Zfs.Tests;

public class ScrubStatusViewModelTests
{
    [Fact]
    public void Constructor_RunningScrubCreatesProgressAndAvailableDetails()
    {
        var model = new ScrubStatusViewModel(new ScrubInfo
        {
            State = "running",
            ProgressPct = 42.125,
            StartTime = "today",
            TimeLeft = "5m",
            Errors = 2,
        });

        Assert.True(model.IsRunning);
        Assert.Equal("Scrub in progress \u2014 42.12% done", model.Headline);
        Assert.Equal(42.125, model.ProgressPercent);
        Assert.Equal(["Started: today", "ETA: 5m", "Errors: 2"], model.Details);
    }

    [Theory]
    [InlineData("finished", "Completed", "text-success", "bi-check-circle")]
    [InlineData("canceled", "Canceled", "text-warning", "bi-dash-circle")]
    [InlineData("idle", "Idle", "text-body-secondary", "bi-clock")]
    public void Constructor_NonRunningScrubCreatesSharedStatusPresentation(
        string state,
        string headline,
        string statusCss,
        string iconCss)
    {
        var model = new ScrubStatusViewModel(new ScrubInfo { State = state });

        Assert.False(model.IsRunning);
        Assert.Equal(headline, model.Headline);
        Assert.Equal(statusCss, model.StatusCss);
        Assert.Equal(iconCss, model.IconCss);
    }
}

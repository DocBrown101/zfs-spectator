using System.Globalization;
using Zfs.Core.Models;
using ZfsDashboard.Models;

namespace ZfsDashboard.Presentation;

public static class ScrubPresentationMapper
{
    public static ScrubStatusViewModel Map(ScrubInfo scrub)
    {
        return scrub.State switch
        {
            "running" => new ScrubStatusViewModel
            {
                IsRunning = true,
                Headline = $"Scrub in progress \u2014 {scrub.ProgressPct.ToString("F2", CultureInfo.InvariantCulture)}% done",
                StatusCss = "text-body-secondary",
                IconCss = "",
                ProgressPercent = scrub.ProgressPct,
                Details = RunningDetails(scrub),
            },
            "finished" => new ScrubStatusViewModel
            {
                Headline = "Completed",
                StatusCss = "text-success",
                IconCss = "bi-check-circle",
                Details =
                [
                    $"Duration: {scrub.Duration}",
                    $"Errors: {scrub.Errors}",
                    scrub.FinishTime,
                ],
            },
            "canceled" => new ScrubStatusViewModel
            {
                Headline = "Canceled",
                StatusCss = "text-warning",
                IconCss = "bi-dash-circle",
                Details = [],
            },
            _ => new ScrubStatusViewModel
            {
                Headline = "Idle",
                StatusCss = "text-body-secondary",
                IconCss = "bi-clock",
                Details = ["No scrub requested."],
            },
        };
    }

    private static IReadOnlyList<string> RunningDetails(ScrubInfo scrub)
    {
        var details = new List<string>();
        if (!string.IsNullOrEmpty(scrub.StartTime)) details.Add($"Started: {scrub.StartTime}");
        if (!string.IsNullOrEmpty(scrub.TimeLeft)) details.Add($"ETA: {scrub.TimeLeft}");
        if (scrub.Errors > 0) details.Add($"Errors: {scrub.Errors}");
        return details;
    }
}

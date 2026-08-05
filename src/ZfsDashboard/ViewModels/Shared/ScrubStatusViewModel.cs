using System.Globalization;
using Zfs.Core.Models;

namespace ZfsDashboard.ViewModels.Shared;

public sealed record ScrubStatusViewModel
{
    public static ScrubStatusViewModel Loading { get; } = new();

    private ScrubStatusViewModel()
    {
        this.IsLoading = true;
        this.Headline = "Loading...";
        this.StatusCss = "text-body-secondary";
        this.IconCss = "bi-hourglass-split";
        this.Details = [];
    }

    public ScrubStatusViewModel(ScrubInfo scrub)
    {
        this.Headline = "Idle";
        this.StatusCss = "text-body-secondary";
        this.IconCss = "bi-clock";
        this.Details = ["No scrub requested."];

        switch (scrub.State)
        {
            case "running":
                this.IsRunning = true;
                this.Headline = $"Scrub in progress \u2014 {scrub.ProgressPct.ToString("F2", CultureInfo.InvariantCulture)}% done";
                this.IconCss = "";
                this.ProgressPercent = scrub.ProgressPct;
                this.Details = RunningDetails(scrub);
                break;
            case "finished":
                this.Headline = "Completed";
                this.StatusCss = "text-success";
                this.IconCss = "bi-check-circle";
                this.Details =
                [
                    $"Duration: {scrub.Duration}",
                    $"Errors: {scrub.Errors}",
                    scrub.FinishTime,
                ];
                break;
            case "canceled":
                this.Headline = "Canceled";
                this.StatusCss = "text-warning";
                this.IconCss = "bi-dash-circle";
                this.Details = [];
                break;
        }
    }

    public bool IsLoading { get; }
    public bool IsRunning { get; }
    public string Headline { get; }
    public string StatusCss { get; }
    public string IconCss { get; }
    public double ProgressPercent { get; }
    public IReadOnlyList<string> Details { get; }

    private static IReadOnlyList<string> RunningDetails(ScrubInfo scrub)
    {
        var details = new List<string>();
        if (!string.IsNullOrEmpty(scrub.StartTime)) details.Add($"Started: {scrub.StartTime}");
        if (!string.IsNullOrEmpty(scrub.TimeLeft)) details.Add($"ETA: {scrub.TimeLeft}");
        if (scrub.Errors > 0) details.Add($"Errors: {scrub.Errors}");
        return details;
    }
}

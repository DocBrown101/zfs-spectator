namespace ZfsDashboard.Models;

public sealed record ScrubStatusViewModel
{
    public static ScrubStatusViewModel Loading { get; } = new()
    {
        IsLoading = true,
        Headline = "Loading...",
        StatusCss = "text-body-secondary",
        IconCss = "bi-hourglass-split",
        Details = [],
    };

    public bool IsLoading { get; init; }
    public bool IsRunning { get; init; }
    public required string Headline { get; init; }
    public required string StatusCss { get; init; }
    public required string IconCss { get; init; }
    public double ProgressPercent { get; init; }
    public required IReadOnlyList<string> Details { get; init; }
}

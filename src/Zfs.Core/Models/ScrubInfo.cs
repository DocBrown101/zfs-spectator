namespace Zfs.Core.Models;

public record ScrubInfo
{
    public static ScrubInfo Idle => new() { State = "idle" };

    public required string State { get; init; } // idle | running | finished | canceled
    public double ProgressPct { get; init; }
    public string TimeLeft { get; init; } = "";
    public string Duration { get; init; } = "";
    public long Errors { get; init; }
    public string StartTime { get; init; } = "";
    public string FinishTime { get; init; } = "";
}

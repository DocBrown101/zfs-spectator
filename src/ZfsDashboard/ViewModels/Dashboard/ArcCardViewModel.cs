using System.Globalization;
using Zfs.Core.Models;
using ZfsDashboard.Presentation;
using ZfsDashboard.ViewModels.Shared;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record ArcCardViewModel
{
    public ArcCardViewModel(ArcStats arc)
    {
        var usagePercent = arc.MaxSize > 0 ? (double)arc.Size / arc.MaxSize * 100 : 0;
        var hitRate = (arc.Hits + arc.Misses) > 0 ? (double)arc.Hits / (arc.Hits + arc.Misses) * 100 : 0;
        var l2HitRate = (arc.L2Hits + arc.L2Misses) > 0 ? (double)arc.L2Hits / (arc.L2Hits + arc.L2Misses) * 100 : 0;

        this.IsVisible = arc.MaxSize > 0;
        this.UsagePercent = usagePercent;
        this.HitRate = hitRate;
        this.HitRateCss = ToArcHitRateCss(hitRate);
        this.L2HitRate = arc.L2Size > 0 ? l2HitRate : null;
        this.L2HitRateCss = arc.L2Size > 0 ? ToL2HitRateCss(l2HitRate) : null;
        this.L2Size = arc.L2Size > 0 ? arc.L2Size.FormatBytes() : null;
        this.Details =
        [
            new("ARC Size", $"{arc.Size.FormatBytes()} / {arc.MaxSize.FormatBytes()}", "arcSize"),
            new(
                "L1 Hit Rate",
                $"{hitRate.ToString("F1", CultureInfo.InvariantCulture)}%",
                "arcHitRate",
                ValueCss: $"{ToArcHitRateCss(hitRate)} fw-semibold"),
            ..(arc.L2Size > 0
                ? new KeyValueRowViewModel[]
                {
                    new(
                        "L2 Hit Rate",
                        $"{l2HitRate.ToString("F1", CultureInfo.InvariantCulture)}% ({arc.L2Size.FormatBytes()})",
                        "l2HitRate",
                        ValueCss: $"{ToL2HitRateCss(l2HitRate)} fw-semibold"),
                }
                : []),
            new("Metadata", arc.MetadataSize.FormatBytes(), "arcMeta", arc.MetadataSize > 0),
            new("Data", arc.DataSize.FormatBytes(), "arcData", arc.DataSize > 0),
            new(
                "MRU / MFU",
                $"{arc.MruSize.FormatBytes()} / {arc.MfuSize.FormatBytes()}",
                "arcMruMfu",
                arc.MruSize > 0 || arc.MfuSize > 0),
        ];
    }

    public bool IsVisible { get; }
    public double UsagePercent { get; }
    public double HitRate { get; }
    public string HitRateCss { get; }
    public double? L2HitRate { get; }
    public string? L2HitRateCss { get; }
    public string? L2Size { get; }
    public IReadOnlyList<KeyValueRowViewModel> Details { get; }

    private static string ToArcHitRateCss(double percentage)
    {
        if (percentage >= 90) return "text-success";
        if (percentage >= 70) return "text-warning";
        return "text-danger";
    }

    private static string ToL2HitRateCss(double percentage) => percentage >= 70 ? "text-success" : "text-warning";
}

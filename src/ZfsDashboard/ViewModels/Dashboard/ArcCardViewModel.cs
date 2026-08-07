using System.Globalization;
using Zfs.Core.Models;
using ZfsDashboard.Presentation;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record ArcCardViewModel
{
    public ArcCardViewModel(ArcStats arc)
    {
        this.IsVisible = arc.MaxSize > 0;
        this.UsagePercent = arc.UsagePercent;
        this.HitRate = arc.HitRate;
        this.HitRateCss = ToArcHitRateCss(arc.HitRate);
        this.L2HitRate = arc.L2Size > 0 ? arc.L2HitRate : null;
        this.L2HitRateCss = arc.L2Size > 0 ? ToL2HitRateCss(arc.L2HitRate) : null;
        this.L2Size = arc.L2Size > 0 ? arc.L2Size.FormatBytes() : null;
        this.Details =
        [
            new("ARC Size", $"{arc.Size.FormatBytes()} / {arc.MaxSize.FormatBytes()}", "arcSize"),
            new(
                "L1 Hit Rate",
                $"{arc.HitRate.ToString("F1", CultureInfo.InvariantCulture)}%",
                "arcHitRate",
                ValueCss: $"{ToArcHitRateCss(arc.HitRate)} fw-semibold"),
            ..(arc.L2Size > 0
                ? new MetricRowViewModel[]
                {
                    new(
                        "L2 Hit Rate",
                        $"{arc.L2HitRate.ToString("F1", CultureInfo.InvariantCulture)}% ({arc.L2Size.FormatBytes()})",
                        "l2HitRate",
                        ValueCss: $"{ToL2HitRateCss(arc.L2HitRate)} fw-semibold"),
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
    public IReadOnlyList<MetricRowViewModel> Details { get; }

    private static string ToArcHitRateCss(double percentage) => percentage >= 90 ? "text-success" : percentage >= 70 ? "text-warning" : "text-danger";
    private static string ToL2HitRateCss(double percentage) => percentage >= 70 ? "text-success" : "text-warning";
}

using System.Globalization;
using Zfs.Core.Models;
using ZfsDashboard.Presentation;
using ZfsDashboard.ViewModels.Shared;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed class ArcCardViewModel(ArcStats arc)
{
    public bool IsVisible => arc.MaxSize > 0;
    public double UsagePercent => Percentage(arc.Size, arc.MaxSize);
    public double HitRate => Percentage(arc.Hits, arc.Hits + arc.Misses);
    public string HitRateText => FormatPercentage(this.HitRate);
    public string HitRateCss => this.HitRate switch
    {
        >= 90 => "text-success",
        >= 70 => "text-warning",
        _ => "text-danger",
    };

    public double? L2HitRate => arc.L2Size > 0 ? Percentage(arc.L2Hits, arc.L2Hits + arc.L2Misses) : null;
    public string? L2HitRateText => this.L2HitRate is { } hitRate ? $"{FormatPercentage(hitRate)} ({arc.L2Size.FormatBytes()})" : null;
    public string? L2HitRateCss => this.L2HitRate switch
    {
        >= 70 => "text-success",
        not null => "text-warning",
        null => null,
    };

    public string? L2Size => arc.L2Size > 0 ? arc.L2Size.FormatBytes() : null;

    public IReadOnlyList<KeyValueRowViewModel> Details
    {
        get
        {
            var rows = new List<KeyValueRowViewModel>
            {
                new("ARC Size", $"{arc.Size.FormatBytes()} / {arc.MaxSize.FormatBytes()}", "arcSize"),
                new("L1 Hit Rate", this.HitRateText, "arcHitRate", ValueCss: $"{this.HitRateCss} fw-semibold"),
            };

            if (this.L2HitRateText is { } l2HitRateText)
            {
                rows.Add(new("L2 Hit Rate", l2HitRateText, "l2HitRate", ValueCss: $"{this.L2HitRateCss} fw-semibold"));
            }

            rows.AddRange(
            [
                new("Metadata", arc.MetadataSize.FormatBytes(), "arcMeta", arc.MetadataSize > 0),
                new("Data", arc.DataSize.FormatBytes(), "arcData", arc.DataSize > 0),
                new("MRU / MFU", $"{arc.MruSize.FormatBytes()} / {arc.MfuSize.FormatBytes()}", "arcMruMfu", arc.MruSize > 0 || arc.MfuSize > 0),
            ]);

            return rows;
        }
    }

    private static double Percentage(ulong value, ulong total) => total > 0 ? (double)value / total * 100 : 0;

    private static string FormatPercentage(double value) => $"{value.ToString("F1", CultureInfo.InvariantCulture)}%";
}

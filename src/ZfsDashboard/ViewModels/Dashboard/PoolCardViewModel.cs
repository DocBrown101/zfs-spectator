using System.Globalization;
using Zfs.Core.Models;
using ZfsDashboard.Presentation;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record PoolCardViewModel
{
    public PoolCardViewModel(Pool pool)
    {
        this.Name = pool.Name;
        this.Health = pool.Health;
        this.HealthCss = pool.Health.ToStatusBadgeCss();
        this.Encrypted = pool.Encrypted;
        this.EncryptionAlgorithm = pool.EncryptionAlgorithm;
        this.HasErrors = pool.HasErrors;
        this.ErrorTooltip = $"R:{pool.ErrorsRead} W:{pool.ErrorsWrite} C:{pool.ErrorsChecksum}";
        this.Size = pool.UsableSize.FormatBytes();
        this.Allocated = pool.UsableUsed.FormatBytes();
        this.Free = pool.UsableAvail.FormatBytes();
        this.UsagePercent = pool.UsagePercent;
        this.CapacityCss = pool.UsagePercent.ToCapacityCss();
    }

    public string Name { get; }
    public string Health { get; }
    public string HealthCss { get; }
    public bool Encrypted { get; }
    public string EncryptionAlgorithm { get; }
    public bool HasErrors { get; }
    public string ErrorTooltip { get; }
    public string Size { get; }
    public string Allocated { get; }
    public string Free { get; }
    public double UsagePercent { get; }
    public double ClampedUsagePercent => Math.Clamp(this.UsagePercent, 0, 100);
    public string UsagePercentText => $"{this.UsagePercent.ToString("F0", CultureInfo.InvariantCulture)}%";
    public string CapacityCss { get; }
}

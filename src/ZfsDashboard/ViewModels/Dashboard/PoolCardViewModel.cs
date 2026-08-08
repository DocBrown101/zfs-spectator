using System.Globalization;
using Zfs.Core.Models;
using ZfsDashboard.Presentation;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record PoolCardViewModel
{
    private readonly Pool pool;

    public PoolCardViewModel(Pool pool)
    {
        this.pool = pool;
    }

    public string Name => this.pool.Name;
    public string Health => this.pool.Health;
    public string HealthCss => this.pool.Health.ToStatusBadgeCss();
    public bool Encrypted => this.pool.Encrypted;
    public string EncryptionAlgorithm => this.pool.EncryptionAlgorithm;
    public bool HasErrors => this.pool.HasErrors;
    public string ErrorTooltip => $"R:{this.pool.ErrorsRead} W:{this.pool.ErrorsWrite} C:{this.pool.ErrorsChecksum}";
    public string Size => this.pool.UsableSize.FormatBytes();
    public string Allocated => this.pool.UsableUsed.FormatBytes();
    public string Free => this.pool.UsableAvail.FormatBytes();
    public double UsagePercent => this.pool.UsagePercent;
    public double ClampedUsagePercent => Math.Clamp(this.UsagePercent, 0, 100);
    public string UsagePercentText => $"{this.UsagePercent.ToString("F0", CultureInfo.InvariantCulture)}%";
    public string CapacityCss => this.pool.UsagePercent.ToCapacityCss();
}

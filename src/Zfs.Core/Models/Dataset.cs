namespace Zfs.Core.Models;

public record Dataset
{
    public required string Name { get; init; }
    public required string ShortName { get; init; }
    public ulong Used { get; init; }
    public ulong Avail { get; init; }
    public ulong Refer { get; init; }
    public ulong Quota { get; init; }
    public ulong RefQuota { get; init; }
    public ulong Refreservation { get; init; }
    public required string Compression { get; init; }
    public required string CompRatio { get; init; }
    public ulong RecordSize { get; init; }
    public required string Mountpoint { get; init; }
    public required string Sync { get; init; }
    public required string Dedup { get; init; }
    public required string CaseSensitivity { get; init; }
    public string Comment { get; init; } = "";
    public int Depth { get; init; }
    public bool Encrypted { get; init; }
    public bool KeyLocked { get; init; }
    public string EncryptionAlgorithm { get; init; } = "";
    public bool Mounted { get; init; }
    public required string CanMount { get; init; }
    public ulong UsedBySnapshots { get; init; }

    public double UsagePercent => (this.Used + this.Avail) > 0 ? (double)this.Used / (this.Used + this.Avail) * 100 : 0;
}

namespace Zfs.Core.Models;

public record ZVol
{
    public required string Name { get; init; }
    public required string Pool { get; init; }
    public ulong Size { get; init; }
    public ulong Used { get; init; }
    public ulong Refer { get; init; }
    public ulong Refreservation { get; init; }
    public required string Compression { get; init; }
    public required string CompRatio { get; init; }
    public required string Sync { get; init; }
    public required string Dedup { get; init; }
    public required string VolBlockSize { get; init; }
    public bool Encrypted { get; init; }
    public string Comment { get; init; } = "";
    public string DevPath => $"/dev/zvol/{this.Name}";
}

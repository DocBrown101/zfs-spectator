namespace Zfs.Core.Models;

public record DiskIoInfo
{
    public string Device { get; init; } = "";
    public ulong ReadsCompleted { get; init; }
    public ulong WritesCompleted { get; init; }
    public ulong SectorsRead { get; init; }
    public ulong SectorsWritten { get; init; }
    public ulong ReadTimeMs { get; init; }
    public ulong WriteTimeMs { get; init; }
    public ulong IoInProgress { get; init; }
    public ulong IoTimeMs { get; init; }
}

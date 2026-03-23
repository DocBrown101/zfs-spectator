using Zfs.Core.Models;

namespace Zfs.Core.Services;

public interface IZfsService
{
    Task<List<Dataset>> GetAllDatasetsAsync();
    Task<List<Dataset>> GetDatasetsAsync(string poolName);
    Task<List<Snapshot>> GetSnapshotsAsync(string poolName);
    Task<List<ZVol>> GetAllZVolsAsync();
    Task<string> GetZfsVersionAsync();
    Task<ArcStats> GetArcStatsAsync();
}

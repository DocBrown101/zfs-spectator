using Zfs.Core.Models;

namespace Zfs.Core.Services;

public interface IZpoolService
{
    Task<List<Pool>> GetAllPoolsAsync();
    Task<List<string>> GetPoolNamesAsync();
    Task<Pool?> GetPoolByNameAsync(string name);
    Task<ScrubInfo> GetScrubStatusAsync(string poolName);
    Task<List<PoolIoSnapshot>> GetAllPoolIoSnapshotsAsync();
}

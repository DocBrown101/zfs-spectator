using Zfs.Core.Models;

namespace Zfs.Core.Services;

public interface IZpoolService
{
    Task<List<Pool>> GetAllPoolsAsync();
    Task<List<(Pool Pool, ScrubInfo Scrub)>> GetAllPoolsWithScrubAsync(CancellationToken cancellationToken = default);
    Task<List<(Pool Pool, ScrubInfo Scrub)>> GetDashboardPoolsAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetPoolNamesAsync();
    Task<(Pool Pool, ScrubInfo Scrub)?> GetPoolWithScrubAsync(string name);
}

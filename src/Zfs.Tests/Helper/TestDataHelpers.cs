namespace Zfs.Tests.Helper;

using Zfs.Core.Models;
using Zfs.Core.Services;

internal static class TestDataHelpers
{
    public static Pool MakePool(string name) => new()
    {
        Name = name,
        Health = "ONLINE",
        VdevType = "mirror",
        Operation = "",
        Compression = "lz4",
        CompRatio = "1.00x",
        Dedup = "off",
        Sync = "standard",
        Atime = "off",
    };

    public sealed class StubZfsService : IZfsService
    {
        public Task<ArcStats> GetArcStatsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ArcStats());
        public Task<List<Dataset>> GetAllDatasetsAsync() => Task.FromResult(new List<Dataset>());
        public Task<List<Dataset>> GetDatasetsAsync(string poolName) => Task.FromResult(new List<Dataset>());
        public Task<List<Snapshot>> GetSnapshotsAsync(string poolName) => Task.FromResult(new List<Snapshot>());
        public Task<List<ZVol>> GetAllZVolsAsync() => Task.FromResult(new List<ZVol>());
        public Task<string> GetZfsVersionAsync(CancellationToken cancellationToken = default) => Task.FromResult("test");
    }
}

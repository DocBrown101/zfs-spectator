using Zfs.Core.Models;
using Zfs.Core.Services;

namespace ZfsDashboard.Services;

public sealed class DashboardSnapshotBackgroundService(
    ISystemService system,
    IZfsService zfs,
    IZpoolService zpool,
    IDiskTemperatureProvider temperatures,
    TimeProvider timeProvider,
    ILogger<DashboardSnapshotBackgroundService> logger) : BackgroundService, IDashboardSnapshotProvider
{
    private static readonly TimeSpan SystemInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PoolInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PoolDetailsInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StaticSystemInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StaticSystemRetryInterval = TimeSpan.FromMinutes(1);

    private readonly TaskCompletionSource<DashboardSnapshot> firstSnapshot = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<StaticSystemInfo> firstStaticSystem = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile DashboardSnapshot? current;
    private IReadOnlyList<(Pool Pool, ScrubInfo Scrub)> poolSnapshots = [];
    private StaticSystemInfo staticSystem = new();
    private DateTimeOffset nextPoolRefresh;
    private DateTimeOffset nextPoolDetailsRefresh;
    private DateTimeOffset nextStaticSystemRefresh;
    private bool poolDetailsInitialized;
    private volatile bool staticSystemInitialized;

    public DashboardSnapshot? Current => this.current;

    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = this.current;
        return snapshot ?? await this.firstSnapshot.Task.WaitAsync(cancellationToken);
    }

    public async Task<StaticSystemInfo> GetStaticSystemInfoAsync(CancellationToken cancellationToken = default)
    {
        if (this.staticSystemInitialized) return this.staticSystem;
        return await this.firstStaticSystem.Task.WaitAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await this.CollectOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(SystemInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await this.CollectOnceAsync(stoppingToken);
    }

    internal async Task CollectOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        if (!this.staticSystemInitialized || now >= this.nextStaticSystemRefresh)
        {
            var refreshSucceeded = false;
            try
            {
                var refreshed = await system.GetStaticSystemInfoAsync(zfs, cancellationToken);
                if (IsValidStaticSystemInfo(refreshed))
                {
                    this.staticSystem = refreshed;
                    refreshSucceeded = true;
                }
                else
                {
                    if (!this.staticSystemInitialized)
                        this.staticSystem = refreshed;
                    logger.LogWarning("Static system information is incomplete; retrying shortly");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to refresh static system information");
            }
            finally
            {
                this.firstStaticSystem.TrySetResult(this.staticSystem);
                this.staticSystemInitialized = true;
                this.nextStaticSystemRefresh = timeProvider.GetUtcNow() +
                    (refreshSucceeded ? StaticSystemInterval : StaticSystemRetryInterval);
            }
        }

        now = timeProvider.GetUtcNow();
        var refreshDetails = !this.poolDetailsInitialized || now >= this.nextPoolDetailsRefresh;
        try
        {
            if (refreshDetails)
            {
                this.poolSnapshots = await zpool.GetAllPoolsWithScrubAsync(cancellationToken);
                this.poolDetailsInitialized = true;
                var completedAt = timeProvider.GetUtcNow();
                this.nextPoolDetailsRefresh = completedAt + PoolDetailsInterval;
                this.nextPoolRefresh = completedAt + PoolInterval;
            }
            else if (now >= this.nextPoolRefresh)
            {
                var runtimePools = await zpool.GetDashboardPoolsAsync(cancellationToken);
                this.poolSnapshots = MergePoolDetails(runtimePools, this.poolSnapshots);
                var completedAt = timeProvider.GetUtcNow();
                this.nextPoolRefresh = completedAt + PoolInterval;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var completedAt = timeProvider.GetUtcNow();
            this.nextPoolRefresh = completedAt + PoolInterval;
            if (refreshDetails)
            {
                this.poolDetailsInitialized = true;
                this.nextPoolDetailsRefresh = completedAt + PoolDetailsInterval;
            }
            logger.LogError(ex, "Failed to refresh dashboard pool data; keeping the previous pool snapshot");
        }

        try
        {
            var data = await system.GetDashboardDataAsync(zfs, this.poolSnapshots, cancellationToken);
            ApplyTemperatures(data.DiskIoRates, temperatures.Temperatures);
            var snapshot = new DashboardSnapshot(
                data,
                this.poolSnapshots.Select(item => item.Pool).ToList().AsReadOnly(),
                this.staticSystem);

            this.current = snapshot;
            this.firstSnapshot.TrySetResult(snapshot);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to collect dashboard data; keeping the previous snapshot");
        }
    }

    private static IReadOnlyList<(Pool Pool, ScrubInfo Scrub)> MergePoolDetails(
        IReadOnlyList<(Pool Pool, ScrubInfo Scrub)> runtimePools,
        IReadOnlyList<(Pool Pool, ScrubInfo Scrub)> detailedPools)
    {
        var detailsByIdentity = detailedPools.ToDictionary(item => PoolIdentity(item.Pool), item => item.Pool, StringComparer.Ordinal);
        return runtimePools.Select(item =>
        {
            if (!detailsByIdentity.TryGetValue(PoolIdentity(item.Pool), out var details))
            {
                var runtime = item.Pool with
                {
                    UsableSize = item.Pool.Size,
                    UsableUsed = item.Pool.Alloc,
                    UsableAvail = item.Pool.Free,
                };
                return (runtime, item.Scrub);
            }

            var merged = item.Pool with
            {
                UsableSize = details.UsableSize,
                UsableUsed = details.UsableUsed,
                UsableAvail = details.UsableAvail,
                Ashift = details.Ashift,
                Compression = details.Compression,
                CompRatio = details.CompRatio,
                Dedup = details.Dedup,
                Sync = details.Sync,
                Atime = details.Atime,
                Encrypted = details.Encrypted,
                KeyLocked = details.KeyLocked,
                EncryptionAlgorithm = details.EncryptionAlgorithm,
                SpecialSize = details.SpecialSize,
                SpecialAlloc = details.SpecialAlloc,
                SpecialFree = details.SpecialFree,
            };
            return (merged, item.Scrub);
        }).ToList().AsReadOnly();
    }

    private static string PoolIdentity(Pool pool)
    {
        var devices = pool.DataDevices
            .Concat(pool.CacheDevices)
            .Concat(pool.LogDevices)
            .Concat(pool.SpareDevices)
            .Concat(pool.SpecialDevices)
            .Select(device => device.Path)
            .Order(StringComparer.Ordinal);
        return $"{pool.Name}|{pool.Size}|{string.Join('|', devices)}";
    }

    private static void ApplyTemperatures(List<DiskIoRateInfo> disks, IReadOnlyDictionary<string, int> values)
    {
        for (var i = 0; i < disks.Count; i++)
        {
            if (values.TryGetValue(disks[i].Device, out var temperature))
                disks[i] = disks[i] with { Temperature = temperature };
        }
    }

    private static bool IsValidStaticSystemInfo(StaticSystemInfo value)
    {
        return value.CpuCount > 0 &&
               IsKnown(value.Hostname) &&
               IsKnown(value.Kernel) &&
               IsKnown(value.ZfsVersion) &&
               IsKnown(value.Processor);
    }

    private static bool IsKnown(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Equals("unknown", StringComparison.OrdinalIgnoreCase);
}

using Microsoft.Extensions.Logging.Abstractions;
using Zfs.Core.Models;
using Zfs.Core.Services;
using Zfs.Tests.Helper;
using ZfsDashboard.Services;

namespace Zfs.Tests;

public class DashboardSnapshotServiceTests
{
    [Fact]
    public async Task CollectOnceAsync_ReusesPoolDataAcrossFastSamples()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero));
        var system = new StubSystemService();
        var zpool = new StubZpoolService([Snapshot(MakePool("tank"), ScrubInfo.Idle)]);
        var service = CreateService(system, zpool, time);

        await service.CollectOnceAsync();
        await service.CollectOnceAsync();

        Assert.Equal(2, system.CollectionCount);
        Assert.Equal(1, zpool.DetailCollectionCount);
        Assert.Equal(0, zpool.RuntimeCollectionCount);
        Assert.Equal(42, service.Current?.Data.DiskIoRates.Single().Temperature);
    }

    [Fact]
    public async Task CollectOnceAsync_UsesLightweightPoolRefreshAfterTenSeconds()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero));
        var detailedPool = MakePool("tank") with { Compression = "zstd", Ashift = 12, UsableSize = 100, UsableUsed = 25, UsableAvail = 75 };
        var runtimePool = MakePool("tank") with { Health = "DEGRADED" };
        var zpool = new StubZpoolService([Snapshot(detailedPool, ScrubInfo.Idle)])
        {
            RuntimeSnapshots = [Snapshot(runtimePool, new ScrubInfo { State = "running" })],
        };
        var service = CreateService(new StubSystemService(), zpool, time);

        await service.CollectOnceAsync();
        time.Advance(TimeSpan.FromSeconds(10));
        await service.CollectOnceAsync();

        var pool = Assert.Single(service.Current!.Pools);
        Assert.Equal("DEGRADED", pool.Health);
        Assert.Equal("zstd", pool.Compression);
        Assert.Equal(12, pool.Ashift);
        Assert.Equal(1, zpool.DetailCollectionCount);
        Assert.Equal(1, zpool.RuntimeCollectionCount);
    }

    [Fact]
    public async Task CollectOnceAsync_NewPoolInRuntimeRefresh_FallsBackToListUsableSizes()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero));
        var detailedPool = MakePool("tank") with { UsableSize = 100, UsableUsed = 25, UsableAvail = 75 };
        var newPoolScrub = new ScrubInfo { State = "running" };
        var runtimePools = new List<(Pool Pool, ScrubInfo Scrub)>
        {
            Snapshot(MakePool("tank"), ScrubInfo.Idle),
            Snapshot(MakePool("newpool") with { Size = 1000, Alloc = 400, Free = 600 }, newPoolScrub),
        };
        var zpool = new StubZpoolService([Snapshot(detailedPool, ScrubInfo.Idle)])
        {
            RuntimeSnapshots = runtimePools,
        };
        var service = CreateService(new StubSystemService(), zpool, time);

        await service.CollectOnceAsync();
        time.Advance(TimeSpan.FromSeconds(10));
        await service.CollectOnceAsync();

        var newPool = Assert.Single(service.Current!.Pools, pool => pool.Name == "newpool");
        Assert.Equal(1000UL, newPool.UsableSize);
        Assert.Equal(400UL, newPool.UsableUsed);
        Assert.Equal(600UL, newPool.UsableAvail);
        Assert.Same(newPoolScrub, service.Current.Data.PoolScrubs["newpool"]);

        var tank = Assert.Single(service.Current.Pools, pool => pool.Name == "tank");
        Assert.Equal(100UL, tank.UsableSize);
    }

    [Fact]
    public async Task CollectOnceAsync_FailedFastSampleKeepsPreviousSnapshot()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero));
        var system = new StubSystemService();
        var service = CreateService(system, new StubZpoolService([]), time);

        await service.CollectOnceAsync();
        var successful = service.Current!;
        system.ThrowOnCollection = true;
        time.Advance(TimeSpan.FromSeconds(1));

        await service.CollectOnceAsync();

        Assert.Same(successful, service.Current);
    }

    [Fact]
    public async Task CollectOnceAsync_NoPoolsDoesNotRepeatDetailRefreshEverySecond()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero));
        var zpool = new StubZpoolService([]);
        var service = CreateService(new StubSystemService(), zpool, time);

        await service.CollectOnceAsync();
        time.Advance(TimeSpan.FromSeconds(1));
        await service.CollectOnceAsync();

        Assert.Equal(1, zpool.DetailCollectionCount);
    }

    [Fact]
    public async Task CollectOnceAsync_FailedPoolRefreshKeepsPoolsAndUsesBackoff()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero));
        var zpool = new StubZpoolService([Snapshot(MakePool("tank"), ScrubInfo.Idle)]);
        var service = CreateService(new StubSystemService(), zpool, time);

        await service.CollectOnceAsync();
        zpool.ThrowOnRuntimeCollection = true;
        time.Advance(TimeSpan.FromSeconds(10));
        await service.CollectOnceAsync();

        Assert.Equal("tank", Assert.Single(service.Current!.Pools).Name);
        Assert.Equal(1, zpool.RuntimeCollectionCount);

        time.Advance(TimeSpan.FromSeconds(1));
        await service.CollectOnceAsync();

        Assert.Equal(1, zpool.RuntimeCollectionCount);
    }

    [Fact]
    public async Task CollectOnceAsync_FailedDetailRefreshKeepsPreviousDetailsDuringRuntimeRefresh()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero));
        var detailedPool = MakePool("tank") with { Compression = "zstd" };
        var runtimePool = MakePool("tank") with { Compression = "runtime" };
        var zpool = new StubZpoolService([Snapshot(detailedPool, ScrubInfo.Idle)])
        {
            RuntimeSnapshots = [Snapshot(runtimePool, ScrubInfo.Idle)],
        };
        var service = CreateService(new StubSystemService(), zpool, time);

        await service.CollectOnceAsync();
        zpool.ThrowOnDetailCollection = true;
        time.Advance(TimeSpan.FromMinutes(1));
        await service.CollectOnceAsync();

        zpool.ThrowOnDetailCollection = false;
        time.Advance(TimeSpan.FromSeconds(10));
        await service.CollectOnceAsync();

        Assert.Equal(1, zpool.RuntimeCollectionCount);
        Assert.Equal("zstd", Assert.Single(service.Current!.Pools).Compression);
    }

    [Fact]
    public async Task CollectOnceAsync_RefreshesStaticDataAfterInterval()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero));
        var system = new StubSystemService();
        var service = CreateService(system, new StubZpoolService([]), time);

        await service.CollectOnceAsync();
        var updated = new StaticSystemInfo
        {
            Hostname = "updated",
            Kernel = "updated-kernel",
            ZfsVersion = "updated-zfs",
            Processor = "updated-cpu",
            CpuCount = 2,
        };
        system.StaticSystem = updated;
        time.Advance(TimeSpan.FromHours(24));

        await service.CollectOnceAsync();

        Assert.Same(updated, service.Current!.StaticSystem);
        Assert.Equal(2, system.StaticCollectionCount);
    }

    [Fact]
    public async Task CollectOnceAsync_UnavailableStaticRefreshKeepsPreviousDataAndRetriesSoon()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero));
        var system = new StubSystemService();
        var service = CreateService(system, new StubZpoolService([]), time);

        await service.CollectOnceAsync();
        var valid = service.Current!.StaticSystem;
        system.StaticSystem = new StaticSystemInfo
        {
            Hostname = "unknown",
            Kernel = "kernel",
            ZfsVersion = "zfs",
            Processor = "cpu",
            CpuCount = 2,
        };
        time.Advance(TimeSpan.FromHours(24));

        await service.CollectOnceAsync();

        Assert.Same(valid, service.Current!.StaticSystem);
        Assert.Equal(2, system.StaticCollectionCount);

        var recovered = valid with { Hostname = "recovered" };
        system.StaticSystem = recovered;
        time.Advance(TimeSpan.FromMinutes(1));
        await service.CollectOnceAsync();

        Assert.Same(recovered, service.Current!.StaticSystem);
        Assert.Equal(3, system.StaticCollectionCount);
    }

    private static DashboardSnapshotBackgroundService CreateService(
        ISystemService system,
        IZpoolService zpool,
        TimeProvider timeProvider) =>
        new(
            system,
            new TestDataHelpers.StubZfsService(),
            zpool,
            new StubTemperatureProvider(),
            timeProvider,
            NullLogger<DashboardSnapshotBackgroundService>.Instance);

    private static (Pool Pool, ScrubInfo Scrub) Snapshot(Pool pool, ScrubInfo scrub) => (pool, scrub);

    private static Pool MakePool(string name) => TestDataHelpers.MakePool(name);

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;
        public override DateTimeOffset GetUtcNow() => this.now;
        public void Advance(TimeSpan duration) => this.now += duration;
    }

    private sealed class StubTemperatureProvider : IDiskTemperatureProvider
    {
        public IReadOnlyDictionary<string, int> Temperatures { get; } = new Dictionary<string, int> { ["sda"] = 42 };
    }

    private sealed class StubSystemService : ISystemService
    {
        public int CollectionCount { get; private set; }
        public int StaticCollectionCount { get; private set; }
        public bool ThrowOnCollection { get; set; }
        public StaticSystemInfo StaticSystem { get; set; } = new()
        {
            Hostname = "test",
            Kernel = "test-kernel",
            ZfsVersion = "test-zfs",
            Processor = "test-cpu",
            CpuCount = 1,
        };

        public Task<DashboardData> GetDashboardDataAsync(
            IZfsService zfs,
            IReadOnlyList<(Pool Pool, ScrubInfo Scrub)> poolSnapshots,
            CancellationToken cancellationToken = default)
        {
            this.CollectionCount++;
            if (this.ThrowOnCollection) throw new InvalidOperationException("collection failed");

            return Task.FromResult(new DashboardData
            {
                System = new SystemInfo { Uptime = "1h" },
                DiskIoRates = [new DiskIoRateInfo { Device = "sda" }],
                PoolDiskIoRates = poolSnapshots.Select(item => new PoolDiskIoGroup { PoolName = item.Pool.Name }).ToList(),
                PoolScrubs = poolSnapshots.ToDictionary(item => item.Pool.Name, item => item.Scrub),
            });
        }

        public Task<StaticSystemInfo> GetStaticSystemInfoAsync(
            IZfsService zfs,
            CancellationToken cancellationToken = default)
        {
            this.StaticCollectionCount++;
            return Task.FromResult(this.StaticSystem);
        }

        public Task<SystemInfo> GetSystemInfoAsync(
            IZfsService zfs,
            CancellationToken cancellationToken = default) => Task.FromResult(new SystemInfo());
    }

    private sealed class StubZpoolService : IZpoolService
    {
        private readonly IReadOnlyList<(Pool Pool, ScrubInfo Scrub)> detailSnapshots;

        public StubZpoolService(IReadOnlyList<(Pool Pool, ScrubInfo Scrub)> detailSnapshots)
        {
            this.detailSnapshots = detailSnapshots;
            this.RuntimeSnapshots = detailSnapshots;
        }

        public IReadOnlyList<(Pool Pool, ScrubInfo Scrub)> RuntimeSnapshots { get; init; }
        public int DetailCollectionCount { get; private set; }
        public int RuntimeCollectionCount { get; private set; }
        public bool ThrowOnRuntimeCollection { get; set; }
        public bool ThrowOnDetailCollection { get; set; }

        public Task<List<(Pool Pool, ScrubInfo Scrub)>> GetAllPoolsWithScrubAsync(CancellationToken cancellationToken = default)
        {
            this.DetailCollectionCount++;
            if (this.ThrowOnDetailCollection) throw new InvalidOperationException("pool detail collection failed");
            return Task.FromResult(this.detailSnapshots.ToList());
        }

        public Task<List<(Pool Pool, ScrubInfo Scrub)>> GetDashboardPoolsAsync(CancellationToken cancellationToken = default)
        {
            this.RuntimeCollectionCount++;
            if (this.ThrowOnRuntimeCollection) throw new InvalidOperationException("pool collection failed");
            return Task.FromResult(this.RuntimeSnapshots.ToList());
        }

        public Task<List<Pool>> GetAllPoolsAsync() => Task.FromResult(this.detailSnapshots.Select(item => item.Pool).ToList());
        public Task<List<string>> GetPoolNamesAsync() => Task.FromResult(this.detailSnapshots.Select(item => item.Pool.Name).ToList());
        public Task<(Pool Pool, ScrubInfo Scrub)?> GetPoolWithScrubAsync(string name)
        {
            foreach (var snapshot in this.detailSnapshots)
            {
                if (snapshot.Pool.Name == name) return Task.FromResult<(Pool, ScrubInfo)?>(snapshot);
            }

            return Task.FromResult<(Pool, ScrubInfo)?>(null);
        }
    }
}

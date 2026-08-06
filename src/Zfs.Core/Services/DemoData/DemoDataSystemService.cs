using Zfs.Core.Models;

namespace Zfs.Core.Services.TestData;

public class DemoDataSystemService : ISystemService
{
    // Demo disk layout matching the demo pool data:
    // miniTank: mirror of sda + sdb
    // zfsPool:  raidz1 of sdc + sdd + sde, special mirror of nvme0n1 + nvme1n1
    private static readonly (string Name, bool IsNvme)[] DemoDisks =
    [
        ("sda", false),
        ("sdb", false),
        ("sdc", false),
        ("sdd", false),
        ("sde", false),
        ("nvme0n1", true),
        ("nvme1n1", true),
    ];

    private static readonly (string Pool, (string Disk, string VdevType)[] Disks)[] DemoPoolDiskMap =
    [
        ("miniTank", [("sda", "mirror"), ("sdb", "mirror")]),
        ("zfsPool",  [("sdc", "raidz1"), ("sdd", "raidz1"), ("sde", "raidz1"), ("nvme0n1", "special"), ("nvme1n1", "special")]),
    ];

    public async Task<DashboardData> GetDashboardDataAsync(
        IZfsService zfs,
        IZpoolService zpool,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var poolsTask = zpool.GetAllPoolsWithScrubAsync(cancellationToken);
        var systemTask = this.GetSystemInfoAsync(zfs, cancellationToken);
        await Task.WhenAll(poolsTask, systemTask);
        return BuildDashboardData(systemTask.Result, poolsTask.Result);
    }

    public async Task<DashboardData> GetDashboardDataAsync(
        IZfsService zfs,
        IReadOnlyList<(Pool Pool, ScrubInfo Scrub)> poolSnapshots,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var system = await this.GetSystemInfoAsync(zfs, cancellationToken);
        return BuildDashboardData(system, poolSnapshots);
    }

    private static DashboardData BuildDashboardData(
        SystemInfo system,
        IReadOnlyList<(Pool Pool, ScrubInfo Scrub)> poolSnapshots)
    {
        var poolScrubs = poolSnapshots.ToDictionary(snapshot => snapshot.Pool.Name, snapshot => snapshot.Scrub);

        var networkRates = new List<NetworkRateInfo>
        {
            new() { Name = "eth0", RxBytesPerSec = 1258291, TxBytesPerSec = 251699 },
        };

        var diskRates = GenerateDiskIoRates();
        var ratesByDevice = diskRates.ToDictionary(d => d.Device);

        var poolDiskRates = DemoPoolDiskMap.Select(m => new PoolDiskIoGroup
        {
            PoolName = m.Pool,
            Disks = m.Disks
                .Where(d => ratesByDevice.ContainsKey(d.Disk))
                .Select(d => ratesByDevice[d.Disk] with { VdevType = d.VdevType })
                .ToList(),
        }).ToList();

        return new DashboardData
        {
            System = system,
            NetworkRates = networkRates,
            DiskIoRates = diskRates,
            PoolDiskIoRates = poolDiskRates,
            PoolScrubs = poolScrubs,
        };
    }

    public async Task<StaticSystemInfo> GetStaticSystemInfoAsync(
        IZfsService zfs,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var zfsVersion = await zfs.GetZfsVersionAsync(cancellationToken);
        return new StaticSystemInfo
        {
            Hostname = "demo-server",
            Kernel = "6.12.8-zfs",
            ZfsVersion = zfsVersion,
            Processor = "AMD Ryzen 7 5800X 8-Core Processor",
            CpuCount = 16,
        };
    }

    public async Task<SystemInfo> GetSystemInfoAsync(
        IZfsService zfs,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var arc = await zfs.GetArcStatsAsync(cancellationToken);
        return new SystemInfo
        {
            Uptime = "14d 7h 32m",
            Arc = arc,
            Memory = new MemoryInfo
            {
                Total = 34359738368,
                Available = 18253611008,
                Used = 16106127360,
                Buffers = 524288000,
                Cached = 8589934592,
                SwapTotal = 8589934592,
                SwapUsed = 0,
                SwapFree = 8589934592,
            },
            CpuUsagePercent = 50.0 + Random.Shared.NextDouble() * 40.0,
        };
    }

    private static List<DiskIoRateInfo> GenerateDiskIoRates()
    {
        var rng = Random.Shared;
        var rates = new List<DiskIoRateInfo>();

        foreach (var (name, isNvme) in DemoDisks)
        {
            var readOps = isNvme ? 200 + rng.NextDouble() * 800 : 10 + rng.NextDouble() * 50;
            var writeOps = isNvme ? 100 + rng.NextDouble() * 400 : 5 + rng.NextDouble() * 30;
            var readBps = readOps * (isNvme ? 4096 : 131072) * (0.8 + rng.NextDouble() * 0.4);
            var writeBps = writeOps * (isNvme ? 4096 : 131072) * (0.8 + rng.NextDouble() * 0.4);
            var readLatMs = isNvme ? 0.05 + rng.NextDouble() * 0.3 : 2.0 + rng.NextDouble() * 8.0;
            var writeLatMs = isNvme ? 0.08 + rng.NextDouble() * 0.5 : 3.0 + rng.NextDouble() * 12.0;
            var util = isNvme ? 2 + rng.NextDouble() * 15 : 8 + rng.NextDouble() * 40;

            rates.Add(new DiskIoRateInfo
            {
                Device = name,
                ReadBytesPerSec = Math.Round(readBps, 1),
                WriteBytesPerSec = Math.Round(writeBps, 1),
                ReadOpsPerSec = Math.Round(readOps, 1),
                WriteOpsPerSec = Math.Round(writeOps, 1),
                ReadLatencyMs = Math.Round(readLatMs, 2),
                WriteLatencyMs = Math.Round(writeLatMs, 2),
                QueueDepth = Math.Round(isNvme ? rng.NextDouble() * 8 : rng.NextDouble() * 3, 1),
                UtilizationPct = Math.Round(util, 1),
            });
        }

        return rates;
    }
}

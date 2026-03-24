namespace Zfs.Tests;

using Zfs.Core.Models;
using Zfs.Core.Services.Parser;

public class ZfsParserTests
{
    // ── Dataset Tests ───────────────────────────────────────────────────

    [Fact]
    public void ParseDatasets_ShouldParseAllDatasets()
    {
        var json = File.ReadAllText("TestData/zfs_list_datasets.json");

        var datasets = ZfsParser.ParseDatasets(json, "zfsPool");

        Assert.Equal(9, datasets.Count);
    }

    [Fact]
    public void ParseDatasets_ShouldParseRootDataset()
    {
        var json = File.ReadAllText("TestData/zfs_list_datasets.json");

        var datasets = ZfsParser.ParseDatasets(json, "zfsPool");
        var root = datasets[0];

        Assert.Equal("zfsPool", root.Name);
        Assert.Equal("zfsPool", root.ShortName);
        Assert.Equal(9309523489840UL, root.Used);
        Assert.Equal(6526155148240UL, root.Avail);
        Assert.Equal(125568UL, root.Refer);
        Assert.Equal(0UL, root.Quota);
        Assert.Equal(0UL, root.RefQuota);
        Assert.Equal("lz4", root.Compression);
        Assert.Equal("1.00x", root.CompRatio);
        Assert.Equal(131072UL, root.RecordSize);
        Assert.Equal("/zfsPool", root.Mountpoint);
        Assert.Equal("standard", root.Sync);
        Assert.Equal("off", root.Dedup);
        Assert.Equal("sensitive", root.CaseSensitivity);
        Assert.Equal(0UL, root.Refreservation);
        Assert.Equal("", root.Comment);
        Assert.Equal(0, root.Depth);
        Assert.False(root.Encrypted);
        Assert.False(root.KeyLocked);
        Assert.Equal("", root.EncryptionAlgorithm);
        Assert.True(root.Mounted);
        Assert.Equal("on", root.CanMount);
        Assert.Equal(0UL, root.UsedBySnapshots);
    }

    [Fact]
    public void ParseDatasets_ShouldCalculateDepthCorrectly()
    {
        var json = File.ReadAllText("TestData/zfs_list_datasets.json");

        var datasets = ZfsParser.ParseDatasets(json, "zfsPool");

        Assert.Equal(0, datasets.First(d => d.Name == "zfsPool").Depth);
        Assert.Equal(1, datasets.First(d => d.Name == "zfsPool/Cloud").Depth);
        Assert.Equal(1, datasets.First(d => d.Name == "zfsPool/Media").Depth);
        Assert.Equal(2, datasets.First(d => d.Name == "zfsPool/Media/Filme").Depth);
        Assert.Equal(2, datasets.First(d => d.Name == "zfsPool/Media/Serien").Depth);
        Assert.Equal(2, datasets.First(d => d.Name == "zfsPool/PC-Daniel/Archiv").Depth);
    }

    [Fact]
    public void ParseDatasets_ShouldParseShortName()
    {
        var json = File.ReadAllText("TestData/zfs_list_datasets.json");

        var datasets = ZfsParser.ParseDatasets(json, "zfsPool");

        Assert.Equal("Filme", datasets.First(d => d.Name == "zfsPool/Media/Filme").ShortName);
        Assert.Equal("Cloud", datasets.First(d => d.Name == "zfsPool/Cloud").ShortName);
        Assert.Equal("Development", datasets.First(d => d.Name == "zfsPool/PC-Daniel/Development").ShortName);
    }

    [Fact]
    public void ParseDatasets_ShouldParseChildDatasetProperties()
    {
        var json = File.ReadAllText("TestData/zfs_list_datasets.json");

        var datasets = ZfsParser.ParseDatasets(json, "zfsPool");
        var dev = datasets.First(d => d.Name == "zfsPool/PC-Daniel/Development");

        Assert.Equal(2457346048UL, dev.Used);
        Assert.Equal("2.01x", dev.CompRatio);
        Assert.Equal(65536UL, dev.RecordSize);
    }

    [Fact]
    public void ParseDatasets_ShouldReturnEmptyForEmptyInput()
    {
        Assert.Empty(ZfsParser.ParseDatasets("", "zfsPool"));
        Assert.Empty(ZfsParser.ParseDatasets("  ", "zfsPool"));
    }

    [Fact]
    public void ParseDatasets_ShouldReturnEmptyForMissingDatasetsKey()
    {
        Assert.Empty(ZfsParser.ParseDatasets("{}", "zfsPool"));
    }

    // ── Snapshot Tests ──────────────────────────────────────────────────

    [Fact]
    public void ParseSnapshots_ShouldParseSnapshot()
    {
        var json = File.ReadAllText("TestData/zfs_list_snapshots.json");

        var snapshots = ZfsParser.ParseSnapshots(json);

        Assert.Single(snapshots);
        var snap = snapshots[0];
        Assert.Equal("zfsPool/Cloud@backup-2026-03-22", snap.Name);
        Assert.Equal("zfsPool/Cloud", snap.DatasetName);
        Assert.Equal("backup-2026-03-22", snap.SnapName);
        Assert.Equal(57344UL, snap.Used);
        Assert.Equal(119759839696UL, snap.Refer);
        Assert.Equal(1774166100, snap.Creation.ToUnixTimeSeconds());
    }

    [Fact]
    public void ParseSnapshots_ShouldReturnEmptyForEmptyInput()
    {
        Assert.Empty(ZfsParser.ParseSnapshots(""));
        Assert.Empty(ZfsParser.ParseSnapshots("  "));
    }

    [Fact]
    public void ParseSnapshots_ShouldReturnEmptyForMissingDatasetsKey()
    {
        Assert.Empty(ZfsParser.ParseSnapshots("{}"));
    }

    [Fact]
    public void ParseSnapshots_ShouldReturnEmptyForEmptyDatasets()
    {
        var json = """{"output_version":{"command":"zfs list"},"datasets":{}}""";

        Assert.Empty(ZfsParser.ParseSnapshots(json));
    }

    // ── ZVol Tests ──────────────────────────────────────────────────────

    [Fact]
    public void ParseZVols_ShouldParseVolume()
    {
        var json = File.ReadAllText("TestData/zfs_list_zvols.json");

        var zvols = ZfsParser.ParseZVols(json);

        Assert.Single(zvols);
        var zvol = zvols[0];
        Assert.Equal("zfsPool/vm-disk", zvol.Name);
        Assert.Equal("zfsPool", zvol.Pool);
        Assert.Equal(107374182400UL, zvol.Size);
        Assert.Equal(5368709120UL, zvol.Used);
        Assert.Equal(5368709120UL, zvol.Refer);
        Assert.Equal("lz4", zvol.Compression);
        Assert.Equal("1.50x", zvol.CompRatio);
        Assert.Equal("standard", zvol.Sync);
        Assert.Equal("off", zvol.Dedup);
        Assert.Equal("16384", zvol.VolBlockSize);
        Assert.True(zvol.Encrypted);
        Assert.Equal("VM Storage", zvol.Comment);
        Assert.Equal(5368709120UL, zvol.Refreservation);
    }

    [Fact]
    public void ParseZVols_ShouldDerivePoolFromName()
    {
        var json = File.ReadAllText("TestData/zfs_list_zvols.json");

        var zvols = ZfsParser.ParseZVols(json);

        Assert.Equal("zfsPool", zvols[0].Pool);
    }

    [Fact]
    public void ParseZVols_ShouldReturnEmptyForEmptyDatasets()
    {
        var json = """{"output_version":{"command":"zfs list"},"datasets":{}}""";

        Assert.Empty(ZfsParser.ParseZVols(json));
    }

    [Fact]
    public void ParseZVols_ShouldReturnEmptyForEmptyInput()
    {
        Assert.Empty(ZfsParser.ParseZVols(""));
        Assert.Empty(ZfsParser.ParseZVols("{}"));
    }

    // ── VDEV Iostat Parsing Tests ────────────────────────────────────────

    private const string IostatSinglePool =
        "poolA\t5368709120\t16106127360\t1234567\t2345678\t12345678900\t23456789000\t100000000000\t200000000000\t50000000000\t100000000000\n" +
        "  mirror-0\t2684354560\t8053063680\t617283\t1172839\t6172839450\t11728394500\t50000000000\t100000000000\t25000000000\t50000000000\n" +
        "    /dev/sda1\t2684354560\t8053063680\t308641\t586419\t3086419725\t5864197250\t25000000000\t50000000000\t12500000000\t25000000000\n" +
        "    /dev/sdb1\t2684354560\t8053063680\t308642\t586420\t3086419725\t5864197250\t25000000000\t50000000000\t12500000000\t25000000000\n" +
        "cache\t-\t-\t-\t-\t-\t-\t-\t-\t-\t-\n" +
        "  /dev/nvme0n1p1\t107374182400\t102005473280\t9876543\t8765432\t98765430000\t87654320000\t1000000000\t2000000000\t500000000\t1000000000\n";

    [Fact]
    public void ParseVdevIostat_ShouldParsePoolWithMirrorAndCache()
    {
        var pools = ZpoolParser.ParseVdevIostat(IostatSinglePool);

        Assert.Single(pools);
        Assert.Equal("poolA", pools[0].PoolName);
        Assert.Equal(3, pools[0].Devices.Count);
    }

    [Fact]
    public void ParseVdevIostat_ShouldSkipGroupVdevs()
    {
        var pools = ZpoolParser.ParseVdevIostat(IostatSinglePool);
        var devicePaths = pools[0].Devices.Select(d => d.DevicePath).ToList();

        Assert.DoesNotContain("mirror-0", devicePaths);
        Assert.Contains("/dev/sda1", devicePaths);
        Assert.Contains("/dev/sdb1", devicePaths);
    }

    [Fact]
    public void ParseVdevIostat_ShouldAssignCorrectRoles()
    {
        var pools = ZpoolParser.ParseVdevIostat(IostatSinglePool);
        var devices = pools[0].Devices;

        Assert.Equal("data", devices.First(d => d.DevicePath == "/dev/sda1").Role);
        Assert.Equal("data", devices.First(d => d.DevicePath == "/dev/sdb1").Role);
        Assert.Equal("cache", devices.First(d => d.DevicePath == "/dev/nvme0n1p1").Role);
    }

    [Fact]
    public void ParseVdevIostat_ShouldParseCumulativeCounters()
    {
        var pools = ZpoolParser.ParseVdevIostat(IostatSinglePool);
        var sda = pools[0].Devices.First(d => d.DevicePath == "/dev/sda1");

        Assert.Equal(308641, sda.ReadOps);
        Assert.Equal(586419, sda.WriteOps);
        Assert.Equal(3086419725, sda.ReadBytes);
        Assert.Equal(5864197250, sda.WriteBytes);
        Assert.Equal(25000000000, sda.TotalWaitReadNs);
        Assert.Equal(50000000000, sda.TotalWaitWriteNs);
        Assert.Equal(12500000000, sda.DiskWaitReadNs);
        Assert.Equal(25000000000, sda.DiskWaitWriteNs);
    }

    [Fact]
    public void ParseVdevIostat_ShouldHandleMultiplePools()
    {
        var output =
            "poolA\t100\t200\t10\t20\t1000\t2000\t100\t200\t50\t100\n" +
            "  /dev/sda\t100\t200\t10\t20\t1000\t2000\t100\t200\t50\t100\n" +
            "poolB\t300\t400\t30\t40\t3000\t4000\t300\t400\t150\t200\n" +
            "  /dev/sdc\t300\t400\t30\t40\t3000\t4000\t300\t400\t150\t200\n";

        var pools = ZpoolParser.ParseVdevIostat(output);

        Assert.Equal(2, pools.Count);
        Assert.Equal("poolA", pools[0].PoolName);
        Assert.Equal("poolB", pools[1].PoolName);
        Assert.Single(pools[0].Devices);
        Assert.Single(pools[1].Devices);
    }

    [Fact]
    public void ParseVdevIostat_ShouldHandleOutputWithoutLatencyColumns()
    {
        var output =
            "poolA\t100\t200\t10\t20\t1000\t2000\n" +
            "  /dev/sda\t100\t200\t10\t20\t1000\t2000\n";

        var pools = ZpoolParser.ParseVdevIostat(output);
        var dev = pools[0].Devices[0];

        Assert.Equal(10, dev.ReadOps);
        Assert.Equal(0, dev.TotalWaitReadNs);
        Assert.Equal(0, dev.DiskWaitWriteNs);
    }

    [Fact]
    public void ParseVdevIostat_ShouldNormalizeSectionNames()
    {
        var output =
            "poolA\t100\t200\t10\t20\t1000\t2000\t0\t0\t0\t0\n" +
            "  /dev/sda\t100\t200\t10\t20\t1000\t2000\t0\t0\t0\t0\n" +
            "logs\t-\t-\t-\t-\t-\t-\t-\t-\t-\t-\n" +
            "  /dev/sdb\t100\t200\t5\t10\t500\t1000\t0\t0\t0\t0\n" +
            "spares\t-\t-\t-\t-\t-\t-\t-\t-\t-\t-\n" +
            "  /dev/sdc\t100\t200\t0\t0\t0\t0\t0\t0\t0\t0\n";

        var pools = ZpoolParser.ParseVdevIostat(output);
        var devices = pools[0].Devices;

        Assert.Equal("data", devices[0].Role);
        Assert.Equal("log", devices[1].Role);
        Assert.Equal("spare", devices[2].Role);
    }

    [Fact]
    public void ParseVdevIostat_ShouldReturnEmptyForEmptyInput()
    {
        Assert.Empty(ZpoolParser.ParseVdevIostat(""));
        Assert.Empty(ZpoolParser.ParseVdevIostat("  "));
    }
}

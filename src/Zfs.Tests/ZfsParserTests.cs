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

    // ── Tab-indented output (real -H flag format) ────────────────────────

    private const string IostatTabIndented =
        "zfsPool\t9498246037504\t500437827584\t1440\t28\t479086551\t187441\t135867147\t1457085\t4440265\t642660\t1947\t1002\t1689\t934943\t128590\t679\t-\t-\n" +
        "\traidz1-0\t9498246037504\t500437827584\t1440\t28\t479086991\t187441\t135866967\t1457085\t4440268\t642660\t1947\t1002\t1689\t934943\t128590501\t-\t-\n" +
        "\t\twwn-0x5000c5008777065b\t0\t0\t354\t5\t95823749\t37406\t87812816\t979023\t1927042\t481442\t1736\t1378\t2112\t576987\t860753\t51\t-\t-\n" +
        "\t\twwn-0x5000c5008776f851\t0\t0\t355\t5\t95823384\t37225\t84857309\t952150\t1875803\t452213\t2072\t775\t768\t575279\t830536\t77\t-\t-\n" +
        "\t\tata-WDC_WD20EZRZ-00Z5HB0_WD-WCC4M5UDEPK1\t0\t0\t329\t6\t95824746\t37685\t100575225\t935708\t2897365\t626297\t2041\t904\t1152\t342134\t98042477\t-\t-\n" +
        "\t\twwn-0x50004cf2070dcd38\t0\t0\t299\t6\t95823818\t37603\t161325283\t519098\t5342844\t275069\t1916\t968\t1632\t277170\t157026624\t-\t-\n" +
        "\t\twwn-0x50014ee1af651273\t0\t0\t101\t5\t95791085\t37521\t520345475\t3857767\t24467762\t1358787\t1950\t987\t1824\t287542\t7450849917\t-\t-\n";

    [Fact]
    public void ParseVdevIostat_ShouldHandleTabIndentation()
    {
        var pools = ZpoolParser.ParseVdevIostat(IostatTabIndented);

        Assert.Single(pools);
        Assert.Equal("zfsPool", pools[0].PoolName);
        Assert.Equal(5, pools[0].Devices.Count);
    }

    [Fact]
    public void ParseVdevIostat_TabIndent_ShouldSkipGroupVdevs()
    {
        var pools = ZpoolParser.ParseVdevIostat(IostatTabIndented);
        var devicePaths = pools[0].Devices.Select(d => d.DevicePath).ToList();

        Assert.DoesNotContain("raidz1-0", devicePaths);
        Assert.Contains("wwn-0x5000c5008777065b", devicePaths);
    }

    [Fact]
    public void ParseVdevIostat_TabIndent_ShouldParseCorrectFieldValues()
    {
        var pools = ZpoolParser.ParseVdevIostat(IostatTabIndented);
        var dev = pools[0].Devices.First(d => d.DevicePath == "wwn-0x5000c5008777065b");

        Assert.Equal(354, dev.ReadOps);
        Assert.Equal(5, dev.WriteOps);
        Assert.Equal(95823749, dev.ReadBytes);
        Assert.Equal(37406, dev.WriteBytes);
        Assert.Equal(87812816, dev.TotalWaitReadNs);
        Assert.Equal(979023, dev.TotalWaitWriteNs);
        Assert.Equal(1927042, dev.DiskWaitReadNs);
        Assert.Equal(481442, dev.DiskWaitWriteNs);
    }

    [Fact]
    public void ParseVdevIostat_TabIndent_ShouldHandleSectionsAndRoles()
    {
        var output =
            "poolA\t100\t200\t10\t20\t1000\t2000\t100\t200\t50\t100\n" +
            "\tmirror-0\t100\t200\t10\t20\t1000\t2000\t100\t200\t50\t100\n" +
            "\t\t/dev/sda\t100\t200\t10\t20\t1000\t2000\t100\t200\t50\t100\n" +
            "cache\t-\t-\t-\t-\t-\t-\t-\t-\t-\t-\n" +
            "\t/dev/nvme0n1\t50\t100\t5\t10\t500\t1000\t50\t100\t25\t50\n";

        var pools = ZpoolParser.ParseVdevIostat(output);

        Assert.Single(pools);
        Assert.Equal(2, pools[0].Devices.Count);
        Assert.Equal("data", pools[0].Devices[0].Role);
        Assert.Equal("cache", pools[0].Devices[1].Role);
    }
}

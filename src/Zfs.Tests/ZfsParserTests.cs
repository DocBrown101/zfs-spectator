namespace Zfs.Tests;

using System.Text.Json;
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

    // Real output from zpool iostat -vlHp (flat format, 18 fields per line).
    // Column order verified against zpool_iostat_full.txt headers:
    //   name, alloc, free, ops_read, ops_write, bw_read, bw_write,
    //   total_wait_read, total_wait_write, disk_wait_read, disk_wait_write,
    //   syncq_wait_read, syncq_wait_write, asyncq_wait_read, asyncq_wait_write,
    //   scrub_wait, trim_wait, rebuild_wait
    private static readonly string IostatRealOutput = File.ReadAllText("TestData/zpool_iostat.txt");

    [Fact]
    public void ParseVdevIostat_ShouldParsePoolWithAllDevices()
    {
        var pools = ZpoolParser.ParseVdevIostat(IostatRealOutput);

        Assert.Single(pools);
        Assert.Equal("zfsPool", pools[0].PoolName);
        Assert.Equal(5, pools[0].Devices.Count);
    }

    [Fact]
    public void ParseVdevIostat_ShouldSkipGroupVdevs()
    {
        var pools = ZpoolParser.ParseVdevIostat(IostatRealOutput);
        var devicePaths = pools[0].Devices.Select(d => d.DevicePath).ToList();

        Assert.DoesNotContain("raidz1-0", devicePaths);
        Assert.DoesNotContain("mirror-1", devicePaths);
        Assert.Contains("wwn-0x50014ee2c06fdd9f-part2", devicePaths);
        Assert.Contains("nvme-eui.001b444a46ea3364", devicePaths);
    }

    [Fact]
    public void ParseVdevIostat_ShouldAssignDataRole()
    {
        var pools = ZpoolParser.ParseVdevIostat(IostatRealOutput);

        // Flat format without section headers assigns all devices to "data"
        Assert.All(pools[0].Devices, d => Assert.Equal("data", d.Role));
    }

    [Fact]
    public void ParseVdevIostat_ShouldParseCumulativeCounters()
    {
        var pools = ZpoolParser.ParseVdevIostat(IostatRealOutput);
        var dev = pools[0].Devices.First(d => d.DevicePath == "wwn-0x50014ee2c06fdd9f-part2");

        Assert.Equal(0, dev.ReadOps);
        Assert.Equal(2, dev.WriteOps);
        Assert.Equal(790, dev.ReadBytes);
        Assert.Equal(2540250, dev.WriteBytes);
        Assert.Equal(12587449, dev.TotalWaitReadNs);
        Assert.Equal(194496843, dev.TotalWaitWriteNs);
        Assert.Equal(12587449, dev.DiskWaitReadNs);
        Assert.Equal(13280149, dev.DiskWaitWriteNs);
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
        "zfsPool\t9498246037504\t500437827584\t1440\t28\t479086551\t187441\t135867147\t1457085\t4440265\t642660\t1947\t1002\t1689\t934943\t128590\t-\t-\n" +
        "\traidz1-0\t9498246037504\t500437827584\t1440\t28\t479086991\t187441\t135866967\t1457085\t4440268\t642660\t1947\t1002\t1689\t934943\t128590501\t-\t-\n" +
        "\t\twwn-0x5000c5008777065b\t0\t0\t354\t5\t95823749\t37406\t87812816\t979023\t1927042\t481442\t1736\t1378\t2112\t576987\t860753\t-\t-\n" +
        "\t\twwn-0x5000c5008776f851\t0\t0\t355\t5\t95823384\t37225\t84857309\t952150\t1875803\t452213\t2072\t775\t768\t575279\t830536\t-\t-\n" +
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

    [Fact]
    public void ParseVdevIostat_ShouldRecognizeDedupSectionHeader()
    {
        var output =
            "poolA\t100\t200\t10\t20\t1000\t2000\t100\t200\t50\t100\n" +
            "\t/dev/sda\t100\t200\t10\t20\t1000\t2000\t100\t200\t50\t100\n" +
            "dedup\t-\t-\t-\t-\t-\t-\t-\t-\t-\t-\n" +
            "\t/dev/sdb\t50\t100\t5\t10\t500\t1000\t50\t100\t25\t50\n";

        var pools = ZpoolParser.ParseVdevIostat(output);

        Assert.Single(pools);
        Assert.Equal("poolA", pools[0].PoolName);
        Assert.Equal(2, pools[0].Devices.Count);
        Assert.Equal("data", pools[0].Devices[0].Role);
        Assert.Equal("dedup", pools[0].Devices[1].Role);
    }

    // ── Flat format (no tab indentation) ────────────────────────────────

    [Fact]
    public void ParseVdevIostat_FlatFormat_ShouldHandleSectionsAndRoles()
    {
        var output =
            "poolA\t100\t200\t10\t20\t1000\t2000\t100\t200\t50\t100\n" +
            "raidz1-0\t100\t200\t10\t20\t1000\t2000\t100\t200\t50\t100\n" +
            "wwn-0x1234\t0\t0\t10\t20\t1000\t2000\t100\t200\t50\t100\n" +
            "cache\t-\t-\t-\t-\t-\t-\t-\t-\t-\t-\n" +
            "nvme0n1\t50\t100\t5\t10\t500\t1000\t50\t100\t25\t50\n";

        var pools = ZpoolParser.ParseVdevIostat(output);

        Assert.Single(pools);
        Assert.Equal(2, pools[0].Devices.Count);
        Assert.Equal("data", pools[0].Devices[0].Role);
        Assert.Equal("cache", pools[0].Devices[1].Role);
    }

    [Fact]
    public void ParseVdevIostat_FlatFormat_MirrorShouldBeSkipped()
    {
        var output =
            "tank\t100\t200\t10\t20\t1000\t2000\t100\t200\t50\t100\n" +
            "mirror-0\t100\t200\t10\t20\t1000\t2000\t100\t200\t50\t100\n" +
            "sda\t0\t0\t10\t20\t1000\t2000\t100\t200\t50\t100\n" +
            "sdb\t0\t0\t10\t20\t1000\t2000\t100\t200\t50\t100\n";

        var pools = ZpoolParser.ParseVdevIostat(output);

        Assert.Single(pools);
        Assert.Equal("tank", pools[0].PoolName);
        Assert.Equal(2, pools[0].Devices.Count);
        Assert.Equal("sda", pools[0].Devices[0].DevicePath);
        Assert.Equal("sdb", pools[0].Devices[1].DevicePath);
    }

    [Fact]
    public void ParseVdevIostat_FlatFormat_DualPoolShouldSplitByKnownNames()
    {
        var output =
            "miniTank\t21095649280\t1971769176064\t0\t0\t0\t0\t0\t0\t0\t0\n" +
            "wwn-0x50014ee2b702ad1b\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\n" +
            "wwn-0x50014ee2b2d93b72\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\n" +
            "zfsPool\t9498245939200\t500437925888\t1478\t33\t478637151\t216065\t202939312\t2012362\t4761478\t1199300\n" +
            "raidz1-0\t9498245939200\t500437925888\t1478\t33\t478636988\t216065\t202939312\t2012362\t4761478\t1199300\n" +
            "wwn-0x5000c5008777065b\t0\t0\t361\t6\t95741779\t43936\t133872288\t951629\t2216043\t475225\n" +
            "wwn-0x5000c5008776f851\t0\t0\t361\t6\t95742037\t42844\t128973719\t950737\t2172519\t465334\n" +
            "ata-WDC_WD20EZRZ-00Z5HB0_WD-WCC4M5UDEPK1\t0\t0\t335\t7\t95741941\t42801\t159472636\t656664\t3221035\t346722\n" +
            "wwn-0x50004cf2070dcd38\t0\t0\t312\t7\t95728273\t42844\t240254258\t458041\t5580534\t278584\n" +
            "wwn-0x50014ee1af651273\t0\t0\t107\t6\t95684198\t43638\t710662660\t7000395\t24436809\t4392612\n";

        var knownPools = new[] { "miniTank", "zfsPool" };
        var pools = ZpoolParser.ParseVdevIostat(output, knownPools);

        Assert.Equal(2, pools.Count);
        Assert.Equal("miniTank", pools[0].PoolName);
        Assert.Equal(2, pools[0].Devices.Count);
        Assert.Equal("zfsPool", pools[1].PoolName);
        Assert.Equal(5, pools[1].Devices.Count);
    }

    [Fact]
    public void ParseVdevIostat_FlatFormat_DualPoolWithoutKnownNames_MisclassifiesSecondPool()
    {
        // Without known pool names, flat format cannot distinguish the second pool
        // from a leaf device when it has non-zero alloc/free values.
        var output =
            "miniTank\t21095649280\t1971769176064\t0\t0\t0\t0\t0\t0\t0\t0\n" +
            "wwn-0x50014ee2b702ad1b\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\n" +
            "zfsPool\t9498245939200\t500437925888\t1478\t33\t478637151\t216065\t202939312\t2012362\t4761478\t1199300\n" +
            "raidz1-0\t9498245939200\t500437925888\t1478\t33\t478636988\t216065\t202939312\t2012362\t4761478\t1199300\n" +
            "wwn-0x5000c5008777065b\t0\t0\t361\t6\t95741779\t43936\t133872288\t951629\t2216043\t475225\n";

        // Without known names: only 1 pool detected (zfsPool absorbed into miniTank)
        var withoutNames = ZpoolParser.ParseVdevIostat(output);
        Assert.Single(withoutNames);
        Assert.Equal("miniTank", withoutNames[0].PoolName);

        // With known names: correctly splits into 2 pools
        var withNames = ZpoolParser.ParseVdevIostat(output, ["miniTank", "zfsPool"]);
        Assert.Equal(2, withNames.Count);
        Assert.Equal("zfsPool", withNames[1].PoolName);
    }

    // ── Error handling ───────────────────────────────────────────────────

    [Fact]
    public void ParseVdevIostat_NonEmptyButNoDevices_ShouldThrowWithDetails()
    {
        var output = "unknownPool";

        var ex = Assert.Throws<FormatException>(() => ZpoolParser.ParseVdevIostat(output));
        Assert.Contains("unknownPool", ex.Message);
    }

    [Fact]
    public void ParseVdevIostat_TooFewFieldsOnly_ShouldThrowWithSkippedLines()
    {
        var output = "garbage\t1\t2\n" +
                     "more\t3\t4\n";

        var ex = Assert.Throws<FormatException>(() => ZpoolParser.ParseVdevIostat(output));
        Assert.Contains("too few fields", ex.Message);
        Assert.Contains("No pools detected", ex.Message);
    }

    // ── JSON Serialization (verifies property names match frontend JS) ───

    [Fact]
    public void DashboardData_ShouldSerializeWithCamelCasePropertyNames()
    {
        var data = new DashboardData
        {
            Text = new() { ["cpuUsage"] = "5.0%" },
            Html = new(),
            NetworkRates = [],
            PoolLatencies =
            [
                new PoolLatencyData
                {
                    PoolName = "tank",
                    Devices =
                    [
                        new VdevLatencyInfo
                        {
                            DevicePath = "/dev/sda",
                            DeviceName = "sda",
                            Role = "data",
                            ReadLatencyMs = 1.5,
                            WriteLatencyMs = 2.0,
                            ReadOpsPerSec = 100,
                            WriteOpsPerSec = 50,
                            ReadBytesPerSec = 1024,
                            WriteBytesPerSec = 512,
                            QueueDepth = 0.5,
                            UtilizationPct = 25.0,
                        },
                    ],
                },
            ],
        };

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(data, options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Top-level properties
        Assert.True(root.TryGetProperty("poolLatencies", out var latencies));
        Assert.True(root.TryGetProperty("networkRates", out _));
        Assert.True(root.TryGetProperty("text", out _));
        Assert.True(root.TryGetProperty("html", out _));

        // Pool-level properties
        var pool = latencies[0];
        Assert.True(pool.TryGetProperty("poolName", out var poolName));
        Assert.Equal("tank", poolName.GetString());
        Assert.True(pool.TryGetProperty("devices", out var devices));

        // Device-level properties (must match JS property access in Index.cshtml)
        var dev = devices[0];
        Assert.True(dev.TryGetProperty("devicePath", out _));
        Assert.True(dev.TryGetProperty("deviceName", out _));
        Assert.True(dev.TryGetProperty("role", out _));
        Assert.True(dev.TryGetProperty("readLatencyMs", out _));
        Assert.True(dev.TryGetProperty("writeLatencyMs", out _));
        Assert.True(dev.TryGetProperty("readOpsPerSec", out _));
        Assert.True(dev.TryGetProperty("writeOpsPerSec", out _));
        Assert.True(dev.TryGetProperty("readBytesPerSec", out _));
        Assert.True(dev.TryGetProperty("writeBytesPerSec", out _));
        Assert.True(dev.TryGetProperty("queueDepth", out _));
        Assert.True(dev.TryGetProperty("utilizationPct", out _));
    }
}

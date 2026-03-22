namespace Zfs.Tests;

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
        Assert.Equal(0UL, snap.Used);
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
}

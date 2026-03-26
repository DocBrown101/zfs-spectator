namespace Zfs.Tests;

using Zfs.Core;
using Zfs.Core.Services;

/// <summary>
/// A fake command executor that returns canned responses based on exact command + arguments match,
/// falling back to substring matching. Later registrations take priority (override earlier ones).
/// </summary>
internal class FakeCommandExecutor : ICommandExecutor
{
    private readonly List<(string Command, string Args, bool Exact, string Response)> responses = [];

    public FakeCommandExecutor On(string command, string args, string response)
    {
        // Insert at front so later registrations win (last-registered-wins override semantics)
        this.responses.Insert(0, (command, args, Exact: true, response));
        return this;
    }

    public FakeCommandExecutor OnContains(string command, string argsContains, string response)
    {
        this.responses.Insert(0, (command, argsContains, Exact: false, response));
        return this;
    }

    public Task<string> ExecuteAsync(string command, string arguments)
    {
        foreach (var (cmd, args, exact, response) in this.responses)
        {
            if (command != cmd) continue;
            if (exact ? arguments == args : arguments.Contains(args))
                return Task.FromResult(response);
        }
        return Task.FromResult("");
    }
}

public class ZpoolServiceTests
{
    // ── Helper: build a FakeCommandExecutor pre-loaded with standard pool responses ──

    private static FakeCommandExecutor CreateExecutorForPool(string poolName = "zfsPool")
    {
        var zpoolListJson = File.ReadAllText("TestData/zpool_list.json");
        var zpoolStatusJson = File.ReadAllText("TestData/zpool_status.json");
        var zpoolAshiftJson = File.ReadAllText("TestData/zpool_get_ashift.json");
        var zfsGetPropsJson = File.ReadAllText("TestData/zfs_get_pool_props.json");

        return new FakeCommandExecutor()
            .On("zpool", $"list -Hpvj -o name,size,alloc,free,health,frag {poolName}", zpoolListJson)
            .On("zpool", "list -Hpvj -o name,size,alloc,free,health,frag", zpoolListJson)
            .On("zpool", "list -Hpj -o name", zpoolListJson)
            .On("zpool", $"status -Pj {poolName}", zpoolStatusJson)
            .On("zpool", $"get -Hpj ashift {poolName}", zpoolAshiftJson)
            .On("zfs", $"get -Hpj used,available,compression,compressratio,dedup,sync,atime,encryption,keystatus {poolName}", zfsGetPropsJson);
    }

    // ── GetPoolNamesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetPoolNamesAsync_ShouldReturnPoolNames()
    {
        var executor = CreateExecutorForPool();
        var service = new ZpoolService(executor);

        var names = await service.GetPoolNamesAsync();

        Assert.Contains("zfsPool", names);
    }

    [Fact]
    public async Task GetPoolNamesAsync_EmptyResponse_ShouldReturnEmpty()
    {
        var executor = new FakeCommandExecutor()
            .On("zpool", "list -Hpj -o name", "");
        var service = new ZpoolService(executor);

        var names = await service.GetPoolNamesAsync();

        Assert.Empty(names);
    }

    // ── GetAllPoolsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAllPoolsAsync_ShouldReturnEnrichedPool()
    {
        var executor = CreateExecutorForPool();
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();

        Assert.Single(pools);
        var pool = pools[0];
        Assert.Equal("zfsPool", pool.Name);
        Assert.Equal(24238647934976UL, pool.Size);
        Assert.Equal(13975181643776UL, pool.Alloc);
        Assert.Equal(10263466291200UL, pool.Free);
        Assert.Equal("ONLINE", pool.Health);
    }

    [Fact]
    public async Task GetAllPoolsAsync_ShouldEnrichWithUsableUsage()
    {
        var executor = CreateExecutorForPool();
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();
        var pool = pools[0];

        Assert.Equal(9309523489840UL, pool.UsableUsed);
        Assert.Equal(6526155148240UL, pool.UsableAvail);
        Assert.Equal(9309523489840UL + 6526155148240UL, pool.UsableSize);
    }

    [Fact]
    public async Task GetAllPoolsAsync_ShouldEnrichWithProperties()
    {
        var executor = CreateExecutorForPool();
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();
        var pool = pools[0];

        Assert.Equal("lz4", pool.Compression);
        Assert.Equal("1.85x", pool.CompRatio);
        Assert.Equal("off", pool.Dedup);
        Assert.Equal("standard", pool.Sync);
        Assert.Equal("off", pool.Atime);
        Assert.Equal(12, pool.Ashift);
    }

    [Fact]
    public async Task GetAllPoolsAsync_ShouldEnrichWithEncryption()
    {
        var executor = CreateExecutorForPool();
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();
        var pool = pools[0];

        Assert.True(pool.Encrypted);
        Assert.False(pool.KeyLocked);
        Assert.Equal("aes-256-gcm", pool.EncryptionAlgorithm);
    }

    [Fact]
    public async Task GetAllPoolsAsync_ShouldEnrichWithLayout()
    {
        var executor = CreateExecutorForPool();
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();
        var pool = pools[0];

        Assert.Equal("raidz1", pool.VdevType);
        Assert.Equal(3, pool.DataDevices.Count);
        Assert.Equal(2, pool.SpecialDevices.Count);
    }

    [Fact]
    public async Task GetAllPoolsAsync_EmptyResponse_ShouldReturnEmpty()
    {
        var executor = new FakeCommandExecutor()
            .On("zpool", "list -Hpvj -o name,size,alloc,free,health,frag", "");
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();

        Assert.Empty(pools);
    }

    [Fact]
    public async Task GetAllPoolsAsync_WhitespaceResponse_ShouldReturnEmpty()
    {
        var executor = new FakeCommandExecutor()
            .On("zpool", "list -Hpvj -o name,size,alloc,free,health,frag", "   ");
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();

        Assert.Empty(pools);
    }

    // ── GetPoolByNameAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetPoolByNameAsync_ShouldReturnEnrichedPool()
    {
        var executor = CreateExecutorForPool();
        var service = new ZpoolService(executor);

        var pool = await service.GetPoolByNameAsync("zfsPool");

        Assert.NotNull(pool);
        Assert.Equal("zfsPool", pool.Name);
        Assert.Equal("lz4", pool.Compression);
        Assert.Equal(12, pool.Ashift);
    }

    [Fact]
    public async Task GetPoolByNameAsync_EmptyResponse_ShouldReturnNull()
    {
        var executor = new FakeCommandExecutor()
            .On("zpool", "list -Hpvj -o name,size,alloc,free,health,frag nonexistent", "");
        var service = new ZpoolService(executor);

        var pool = await service.GetPoolByNameAsync("nonexistent");

        Assert.Null(pool);
    }

    [Fact]
    public async Task GetPoolByNameAsync_ValidJsonNoMatchingPool_ShouldReturnNull()
    {
        // JSON with a different pool name than the one requested
        var json = """{"output_version":{"command":"zpool list"},"pools":{}}""";
        var executor = new FakeCommandExecutor()
            .On("zpool", "list -Hpvj -o name,size,alloc,free,health,frag otherPool", json);
        var service = new ZpoolService(executor);

        var pool = await service.GetPoolByNameAsync("otherPool");

        Assert.Null(pool);
    }

    // ── GetScrubStatusAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetScrubStatusAsync_FinishedScrub_ShouldReturnFinishedState()
    {
        var zpoolStatusJson = File.ReadAllText("TestData/zpool_status.json");
        var executor = new FakeCommandExecutor()
            .On("zpool", "status -Pj zfsPool", zpoolStatusJson);
        var service = new ZpoolService(executor);

        var scrub = await service.GetScrubStatusAsync("zfsPool");

        Assert.Equal("finished", scrub.State);
        Assert.Equal(0, scrub.Errors);
        Assert.NotEmpty(scrub.StartTime);
        Assert.NotEmpty(scrub.FinishTime);
    }

    [Fact]
    public async Task GetScrubStatusAsync_RunningScrub_ShouldFetchTimeLeft()
    {
        var zpoolStatusScanningJson = File.ReadAllText("TestData/zpool_status_scanning.json");
        var textOutput =
            "  pool: zfsPool\n" +
            " state: ONLINE\n" +
            "  scan: scrub in progress since Mon Mar 23 09:07:04 2026\n" +
            "        7.65T / 8.64T scanned, 6.50T issued at 1.23G/s\n" +
            "        0B repaired, 88.55% done, 0 days 01:23:45 to go\n";

        var executor = new FakeCommandExecutor()
            .On("zpool", "status -Pj zfsPool", zpoolStatusScanningJson)
            .On("zpool", "status zfsPool", textOutput);
        var service = new ZpoolService(executor);

        var scrub = await service.GetScrubStatusAsync("zfsPool");

        Assert.Equal("running", scrub.State);
        Assert.True(scrub.ProgressPct > 0);
        Assert.Equal("01:23:45", scrub.TimeLeft);
    }

    [Fact]
    public async Task GetScrubStatusAsync_RunningScrub_NoTimeInText_ShouldHaveEmptyTimeLeft()
    {
        var zpoolStatusScanningJson = File.ReadAllText("TestData/zpool_status_scanning.json");
        var executor = new FakeCommandExecutor()
            .On("zpool", "status -Pj zfsPool", zpoolStatusScanningJson)
            .On("zpool", "status zfsPool", "  scan: scrub in progress\n");
        var service = new ZpoolService(executor);

        var scrub = await service.GetScrubStatusAsync("zfsPool");

        Assert.Equal("running", scrub.State);
        Assert.Equal("", scrub.TimeLeft);
    }

    // ── Encryption edge cases ────────────────────────────────────────────

    [Fact]
    public async Task GetAllPoolsAsync_UnencryptedPool_ShouldSetEncryptedFalse()
    {
        var executor = CreateExecutorForPool();
        executor.On("zfs", "get -Hpj used,available,compression,compressratio,dedup,sync,atime,encryption,keystatus zfsPool",
            BuildZfsGetJson("zfsPool", ("encryption", "off"), ("keystatus", "-")));
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();

        Assert.False(pools[0].Encrypted);
        Assert.Equal("", pools[0].EncryptionAlgorithm);
    }

    [Fact]
    public async Task GetAllPoolsAsync_LockedKey_ShouldSetKeyLockedTrue()
    {
        var executor = CreateExecutorForPool();
        executor.On("zfs", "get -Hpj used,available,compression,compressratio,dedup,sync,atime,encryption,keystatus zfsPool",
            BuildZfsGetJson("zfsPool", ("encryption", "aes-256-gcm"), ("keystatus", "unavailable")));
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();

        Assert.True(pools[0].Encrypted);
        Assert.True(pools[0].KeyLocked);
    }

    // ── Pool root properties edge cases ──────────────────────────────────

    [Fact]
    public async Task GetAllPoolsAsync_MalformedPropsOutput_ShouldDefaultToZero()
    {
        var executor = CreateExecutorForPool();
        executor.On("zfs", "get -Hpj used,available,compression,compressratio,dedup,sync,atime,encryption,keystatus zfsPool",
            "not valid json {{{");
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();

        Assert.Equal(0UL, pools[0].UsableUsed);
        Assert.Equal(0UL, pools[0].UsableAvail);
    }

    [Fact]
    public async Task GetAllPoolsAsync_PartialPropsOutput_ShouldUseDefaults()
    {
        var executor = CreateExecutorForPool();
        executor.On("zfs", "get -Hpj used,available,compression,compressratio,dedup,sync,atime,encryption,keystatus zfsPool",
            BuildZfsGetJson("zfsPool", ("compression", "zstd")));
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();

        Assert.Equal("zstd", pools[0].Compression);
        Assert.Equal("1.00x", pools[0].CompRatio); // default
        Assert.Equal("off", pools[0].Dedup);        // default
    }

    // ── Special VDEV size ────────────────────────────────────────────────

    [Fact]
    public async Task GetAllPoolsAsync_WithSpecialVdev_ShouldParseSpecialSize()
    {
        var executor = CreateExecutorForPool();
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();
        var pool = pools[0];

        // Values from zpool_list.json special mirror-1
        Assert.Equal(255550554112UL, pool.SpecialSize);
        Assert.Equal(5491740672UL, pool.SpecialAlloc);
        Assert.Equal(250058813440UL, pool.SpecialFree);
    }

    // ── GetAllPoolsVdevDataAsync (delta calculation) ─────────────────────

    [Fact]
    public async Task GetAllPoolsVdevDataAsync_FirstCall_ShouldReturnZeroRates()
    {
        var iostatOutput =
            "tank\t100\t200\t1000\t2000\t100000\t200000\t10000\t20000\t5000\t10000\n" +
            "  /dev/sda\t100\t200\t1000\t2000\t100000\t200000\t10000\t20000\t5000\t10000\n";

        var executor = new FakeCommandExecutor()
            .On("zpool", "iostat -vlHp", iostatOutput);
        var service = new ZpoolService(executor);

        var result = await service.GetAllPoolsVdevDataAsync();

        Assert.Single(result);
        var dev = result[0].Devices[0];
        Assert.Equal(0, dev.ReadOpsPerSec);
        Assert.Equal(0, dev.WriteOpsPerSec);
        Assert.Equal(0, dev.ReadLatencyMs);
        Assert.Equal(0, dev.WriteLatencyMs);
        Assert.Equal(0, dev.QueueDepth);
        Assert.Equal(0, dev.UtilizationPct);
    }

    [Fact]
    public async Task GetAllPoolsVdevDataAsync_SecondCall_ShouldComputeNonZeroRates()
    {
        // Cumulative counters that increase between calls
        var output1 =
            "tank\t100\t200\t1000\t2000\t100000\t200000\t10000000000\t20000000000\t5000000000\t10000000000\n" +
            "  /dev/sda\t100\t200\t1000\t2000\t100000\t200000\t10000000000\t20000000000\t5000000000\t10000000000\n";
        var output2 =
            "tank\t100\t200\t2000\t4000\t200000\t400000\t20000000000\t40000000000\t10000000000\t20000000000\n" +
            "  /dev/sda\t100\t200\t2000\t4000\t200000\t400000\t20000000000\t40000000000\t10000000000\t20000000000\n";

        var callCount = 0;
        var executor = new SequentialExecutor(
            "zpool", "iostat -vlHp",
            () => ++callCount <= 1 ? output1 : output2);
        var service = new ZpoolService(executor);

        // First call: establishes baseline
        await service.GetAllPoolsVdevDataAsync();
        // Second call: computes deltas
        var result = await service.GetAllPoolsVdevDataAsync();

        var dev = result[0].Devices[0];
        Assert.True(dev.ReadOpsPerSec > 100, $"ReadOpsPerSec was {dev.ReadOpsPerSec}, expected > 100");
        Assert.True(dev.WriteOpsPerSec > 100, $"WriteOpsPerSec was {dev.WriteOpsPerSec}, expected > 100");
        Assert.True(dev.ReadBytesPerSec > 1000, $"ReadBytesPerSec was {dev.ReadBytesPerSec}, expected > 1000");
        Assert.True(dev.WriteBytesPerSec > 1000, $"WriteBytesPerSec was {dev.WriteBytesPerSec}, expected > 1000");
    }

    [Fact]
    public async Task GetAllPoolsVdevDataAsync_ShouldAssignDeviceNamesAndRoles()
    {
        var iostatOutput =
            "tank\t100\t200\t10\t20\t1000\t2000\t100\t200\t50\t100\n" +
            "  /dev/sda\t100\t200\t10\t20\t1000\t2000\t100\t200\t50\t100\n" +
            "cache\t-\t-\t-\t-\t-\t-\t-\t-\t-\t-\n" +
            "  /dev/nvme0n1\t50\t100\t5\t10\t500\t1000\t50\t100\t25\t50\n";

        var executor = new FakeCommandExecutor()
            .On("zpool", "iostat -vlHp", iostatOutput);
        var service = new ZpoolService(executor);

        var result = await service.GetAllPoolsVdevDataAsync();

        Assert.Equal("tank", result[0].PoolName);
        Assert.Equal(2, result[0].Devices.Count);
        Assert.Equal("data", result[0].Devices[0].Role);
        Assert.Equal("sda", result[0].Devices[0].DeviceName);
        Assert.Equal("cache", result[0].Devices[1].Role);
        Assert.Equal("nvme0n1", result[0].Devices[1].DeviceName);
    }

    [Fact]
    public async Task GetAllPoolsVdevDataAsync_UtilizationPct_ShouldCapAt100()
    {
        // Huge disk wait values to force utilization > 100%
        var output1 =
            "tank\t100\t200\t0\t0\t0\t0\t0\t0\t0\t0\n" +
            "  /dev/sda\t100\t200\t0\t0\t0\t0\t0\t0\t0\t0\n";
        // Disk wait values exceeding wall-clock time
        var output2 =
            "tank\t100\t200\t1000\t1000\t100000\t100000\t999999999999999\t999999999999999\t999999999999999\t999999999999999\n" +
            "  /dev/sda\t100\t200\t1000\t1000\t100000\t100000\t999999999999999\t999999999999999\t999999999999999\t999999999999999\n";

        var callCount = 0;
        var executor = new SequentialExecutor(
            "zpool", "iostat -vlHp",
            () => ++callCount <= 1 ? output1 : output2);
        var service = new ZpoolService(executor);

        await service.GetAllPoolsVdevDataAsync();
        var result = await service.GetAllPoolsVdevDataAsync();

        Assert.Equal(100.0, result[0].Devices[0].UtilizationPct);
    }

    [Fact]
    public async Task GetAllPoolsVdevDataAsync_EmptyOutput_ShouldReturnEmpty()
    {
        var executor = new FakeCommandExecutor()
            .On("zpool", "iostat -vlHp", "");
        var service = new ZpoolService(executor);

        var result = await service.GetAllPoolsVdevDataAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllPoolsVdevDataAsync_FlatFormat_ShouldParseDevices()
    {
        var iostatOutput =
            "zfsPool\t9498246504448\t500437360640\t1478\t33\t478637151\t216065\t202939312\t2012362\t4761478\t1199300\t1764\t1015\t1740\t972507\t195123305\t-\t-\n" +
            "raidz1-0\t9498246504448\t500437360640\t1478\t33\t478636988\t216065\t202939312\t2012362\t4761478\t1199300\t1764\t1015\t1740\t972507\t195123305\t-\t-\n" +
            "wwn-0x5000c5008777065b\t0\t0\t361\t6\t95741779\t43936\t133872288\t951629\t2216043\t475225\t1621\t1565\t2016\t536282\t131877267\t-\t-\n" +
            "wwn-0x5000c5008776f851\t0\t0\t361\t6\t95742037\t42844\t128973719\t950737\t2172519\t465334\t1688\t762\t1280\t536658\t125089897\t-\t-\n";

        var executor = new FakeCommandExecutor()
            .On("zpool", "iostat -vlHp", iostatOutput);
        var service = new ZpoolService(executor);

        var result = await service.GetAllPoolsVdevDataAsync();

        Assert.Single(result);
        Assert.Equal("zfsPool", result[0].PoolName);
        Assert.Equal(2, result[0].Devices.Count);
        Assert.Equal("wwn-0x5000c5008777065b", result[0].Devices[0].DevicePath);
        Assert.Equal("wwn-0x5000c5008776f851", result[0].Devices[1].DevicePath);
    }

    [Fact]
    public async Task GetAllPoolsVdevDataAsync_UnparseableOutput_ShouldThrow()
    {
        var iostatOutput = "unknownPool";

        var executor = new FakeCommandExecutor()
            .On("zpool", "iostat -vlHp", iostatOutput);
        var service = new ZpoolService(executor);

        await Assert.ThrowsAsync<FormatException>(() => service.GetAllPoolsVdevDataAsync());
    }

    // ── Helper: build a zfs get JSON response with selective property overrides ──

    private static string BuildZfsGetJson(string poolName, params (string Name, string Value)[] properties)
    {
        var propsJson = string.Join(",\n", properties.Select(p =>
            $$"""
                    "{{p.Name}}": {
                      "value": "{{p.Value}}",
                      "source": { "type": "LOCAL", "data": "-" }
                    }
            """));

        return $$"""
            {
              "output_version": { "command": "zfs get", "vers_major": 0, "vers_minor": 1 },
              "datasets": {
                "{{poolName}}": {
                  "name": "{{poolName}}",
                  "type": "FILESYSTEM",
                  "pool": "{{poolName}}",
                  "createtxg": "1",
                  "properties": {
            {{propsJson}}
                  }
                }
              }
            }
            """;
    }
}

/// <summary>
/// An executor that returns different responses on successive calls for a specific command+args pair.
/// Returns empty string for unmatched commands.
/// </summary>
internal class SequentialExecutor(string expectedCommand, string expectedArgs, Func<string> responseFactory) : ICommandExecutor
{
    public Task<string> ExecuteAsync(string command, string arguments)
    {
        if (command == expectedCommand && arguments == expectedArgs)
            return Task.FromResult(responseFactory());
        return Task.FromResult("");
    }
}

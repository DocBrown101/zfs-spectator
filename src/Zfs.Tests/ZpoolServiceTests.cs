namespace Zfs.Tests;

using Zfs.Core;
using Zfs.Core.Services;
using Zfs.Tests.Helper;

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
            .On("zpool", "status -Pj miniTank", zpoolStatusJson)
            .On("zpool", "get -Hpj ashift miniTank", zpoolAshiftJson)
            .On("zfs", "get -Hpj used,available,compression,compressratio,dedup,sync,atime,encryption,keystatus miniTank", zfsGetPropsJson)
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

        Assert.Equal(2, names.Count);
        Assert.Contains("miniTank", names);
        Assert.Contains("zfsPool", names);
    }

    [Fact]
    public async Task GetPoolNamesAsync_EmptyResponse_ShouldReturnEmpty()
    {
        var executor = new FakeCommandExecutor()
            .On("zpool", "list -Hpvj -o name,size,alloc,free,health,frag", "");
        var service = new ZpoolService(executor);

        var names = await service.GetPoolNamesAsync();

        Assert.Empty(names);
        Assert.Single(executor.Invocations);
        Assert.Equal("zpool", executor.Invocations[0].Command);
        Assert.Equal("list -Hpvj -o name,size,alloc,free,health,frag", executor.Invocations[0].Arguments);
    }

    [Fact]
    public async Task GetPoolNamesAsync_AfterListPools_ShouldReturnFromCache()
    {
        var executor = CreateExecutorForPool();
        var service = new ZpoolService(executor);

        // Warm the cache via GetAllPoolsAsync (which calls ListPoolsAsync internally)
        await service.GetAllPoolsAsync();

        // Cache is now populated — this call should not hit the executor
        var names = await service.GetPoolNamesAsync();

        Assert.Equal(2, names.Count);
        Assert.Contains("miniTank", names);
        Assert.Contains("zfsPool", names);
    }

    // ── GetAllPoolsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAllPoolsAsync_ShouldReturnEnrichedPool()
    {
        var executor = CreateExecutorForPool();
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();

        Assert.Equal(2, pools.Count);
        var pool = pools.Single(p => p.Name == "zfsPool");
        Assert.Equal(9998683865088UL, pool.Size);
        Assert.Equal(9498245939200UL, pool.Alloc);
        Assert.Equal(500437925888UL, pool.Free);
        Assert.Equal("ONLINE", pool.Health);
    }

    [Fact]
    public async Task GetAllPoolsWithScrubAsync_ShouldReuseTheEnrichmentStatus()
    {
        var executor = CreateExecutorForPool();
        var service = new ZpoolService(executor);

        var snapshots = await service.GetAllPoolsWithScrubAsync();

        var snapshot = snapshots.Single(item => item.Pool.Name == "zfsPool");
        Assert.Equal("finished", snapshot.Scrub.State);
        Assert.Single(executor.Invocations, call =>
            call.Command == "zpool" && call.Arguments == "status -Pj zfsPool");
    }

    [Fact]
    public async Task GetDashboardPoolsAsync_ShouldSkipExpensivePoolProperties()
    {
        var executor = CreateExecutorForPool();
        var service = new ZpoolService(executor);

        var snapshots = await service.GetDashboardPoolsAsync();

        Assert.Equal(2, snapshots.Count);
        Assert.DoesNotContain(executor.Invocations, call => call.Command == "zfs");
        Assert.DoesNotContain(executor.Invocations, call => call.Arguments.Contains("ashift", StringComparison.Ordinal));
        Assert.Equal(2, executor.Invocations.Count(call =>
            call.Command == "zpool" && call.Arguments.StartsWith("status -Pj", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task GetDashboardPoolsAsync_ShouldKeepSpecialVdevCapacity()
    {
        const string listJson = """
            {
              "pools": {
                "zfsPool": {
                  "name": "zfsPool",
                  "properties": {
                    "size": { "value": "1000" },
                    "allocated": { "value": "400" },
                    "free": { "value": "600" },
                    "health": { "value": "ONLINE" },
                    "fragmentation": { "value": "0" }
                  },
                  "vdevs": {
                    "special": {
                      "mirror-1": {
                        "properties": {
                          "size": { "value": "300" },
                          "allocated": { "value": "100" },
                          "free": { "value": "200" }
                        }
                      }
                    }
                  }
                }
              }
            }
            """;
        var executor = CreateExecutorForPool()
            .On("zpool", "list -Hpvj -o name,size,alloc,free,health,frag", listJson);
        var service = new ZpoolService(executor);

        var pool = Assert.Single(await service.GetDashboardPoolsAsync()).Pool;

        Assert.Equal(300UL, pool.SpecialSize);
        Assert.Equal(100UL, pool.SpecialAlloc);
        Assert.Equal(200UL, pool.SpecialFree);
    }

    [Fact]
    public async Task GetDashboardPoolsAsync_MissingPoolStatus_ShouldFail()
    {
        var executor = CreateExecutorForPool()
            .On("zpool", "status -Pj zfsPool", "{\"pools\":{}}");
        var service = new ZpoolService(executor);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetDashboardPoolsAsync());
    }

    [Fact]
    public async Task GetDashboardPoolsAsync_EmptyPoolStatus_ShouldFail()
    {
        var executor = CreateExecutorForPool()
            .On("zpool", "status -Pj zfsPool", "{\"pools\":{\"zfsPool\":{}}}");
        var service = new ZpoolService(executor);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetDashboardPoolsAsync());
    }

    [Fact]
    public async Task GetDashboardPoolsAsync_EmptySuccessfulOutput_ShouldRepresentNoPools()
    {
        var executor = CreateExecutorForPool()
            .On("zpool", "list -Hpvj -o name,size,alloc,free,health,frag", "");
        var service = new ZpoolService(executor);

        var snapshots = await service.GetDashboardPoolsAsync();

        Assert.Empty(snapshots);
    }

    [Fact]
    public async Task GetDashboardPoolsAsync_IncompleteList_ShouldFail()
    {
        var executor = CreateExecutorForPool()
            .On("zpool", "list -Hpvj -o name,size,alloc,free,health,frag", "{}");
        var service = new ZpoolService(executor);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetDashboardPoolsAsync());
    }

    [Fact]
    public async Task GetDashboardPoolsAsync_ListWithMissingProperties_ShouldFail()
    {
        const string listJson = "{\"pools\":{\"tank\":{\"name\":\"tank\",\"properties\":{}}}}";
        var executor = CreateExecutorForPool()
            .On("zpool", "list -Hpvj -o name,size,alloc,free,health,frag", listJson);
        var service = new ZpoolService(executor);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetDashboardPoolsAsync());
    }

    [Fact]
    public async Task GetAllPoolsWithScrubAsync_RunningScrub_ShouldIncludeTimeLeft()
    {
        var zpoolStatusScanningJson = File.ReadAllText("TestData/zpool_status_scanning.json");
        var executor = CreateExecutorForPool()
            .On("zpool", "status -Pj zfsPool", zpoolStatusScanningJson)
            .On("zpool", "status zfsPool", "  scan: scrub in progress, 0 days 01:23:45 to go\n");
        var service = new ZpoolService(executor);

        var snapshots = await service.GetAllPoolsWithScrubAsync();

        var scrub = snapshots.Single(item => item.Pool.Name == "zfsPool").Scrub;
        Assert.Equal("running", scrub.State);
        Assert.Equal("01:23:45", scrub.TimeLeft);
        Assert.Single(executor.Invocations, call =>
            call.Command == "zpool" && call.Arguments == "status zfsPool");
    }

    [Fact]
    public async Task GetAllPoolsWithScrubAsync_ShouldEnrichPoolsConcurrently()
    {
        var executor = new ConcurrentPoolStatusExecutor(
            File.ReadAllText("TestData/zpool_list.json"),
            File.ReadAllText("TestData/zpool_status.json"),
            File.ReadAllText("TestData/zpool_get_ashift.json"),
            File.ReadAllText("TestData/zfs_get_pool_props.json"));
        var service = new ZpoolService(executor);

        var snapshots = await service.GetAllPoolsWithScrubAsync();

        Assert.Equal(2, snapshots.Count);
        Assert.Equal(2, executor.StatusInvocationCount);
    }

    [Fact]
    public async Task GetDashboardPoolsAsync_RunningScrub_ShouldNotFetchScrubTimeLeft()
    {
        var zpoolStatusScanningJson = File.ReadAllText("TestData/zpool_status_scanning.json");
        var executor = CreateExecutorForPool()
            .On("zpool", "status -Pj zfsPool", zpoolStatusScanningJson);
        var service = new ZpoolService(executor);

        var snapshots = await service.GetDashboardPoolsAsync();

        var scrub = snapshots.Single(item => item.Pool.Name == "zfsPool").Scrub;
        Assert.Equal("running", scrub.State);
        Assert.Empty(scrub.TimeLeft);
        Assert.DoesNotContain(executor.Invocations, call =>
            call.Command == "zpool" && call.Arguments == "status zfsPool");
    }

    // ── GetPoolWithScrubAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetPoolWithScrubAsync_ShouldReturnEnrichedPoolWithScrub()
    {
        var executor = CreateExecutorForPool()
            .On("zpool", "status zfsPool", "");
        var service = new ZpoolService(executor);

        var result = await service.GetPoolWithScrubAsync("zfsPool");

        Assert.NotNull(result);
        Assert.Equal("zfsPool", result.Value.Pool.Name);
        Assert.Equal("lz4", result.Value.Pool.Compression);
        Assert.Equal(12, result.Value.Pool.Ashift);
        Assert.Equal("finished", result.Value.Scrub.State);
        Assert.Equal(0, result.Value.Scrub.Errors);
        Assert.NotEmpty(result.Value.Scrub.StartTime);
        Assert.NotEmpty(result.Value.Scrub.FinishTime);
    }

    [Fact]
    public async Task GetPoolWithScrubAsync_UnknownPool_ShouldReturnNull()
    {
        var executor = CreateExecutorForPool();
        var service = new ZpoolService(executor);

        var result = await service.GetPoolWithScrubAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPoolWithScrubAsync_RunningScrub_ShouldFetchTimeLeft()
    {
        var zpoolStatusScanningJson = File.ReadAllText("TestData/zpool_status_scanning.json");
        var executor = CreateExecutorForPool()
            .On("zpool", "status -Pj zfsPool", zpoolStatusScanningJson)
            .On("zpool", "status zfsPool",
                "  scan: scrub in progress since Mon Mar 23 09:07:04 2026\n" +
                "        7.65T / 8.64T scanned, 6.50T issued at 1.23G/s\n" +
                "        0B repaired, 88.55% done, 0 days 01:23:45 to go\n");
        var service = new ZpoolService(executor);

        var result = await service.GetPoolWithScrubAsync("zfsPool");

        Assert.NotNull(result);
        Assert.Equal("running", result.Value.Scrub.State);
        Assert.True(result.Value.Scrub.ProgressPct > 0);
        Assert.Equal("01:23:45", result.Value.Scrub.TimeLeft);
    }

    [Fact]
    public async Task GetPoolWithScrubAsync_RunningScrub_NoTimeInText_ShouldKeepEmptyTimeLeft()
    {
        var zpoolStatusScanningJson = File.ReadAllText("TestData/zpool_status_scanning.json");
        var executor = CreateExecutorForPool()
            .On("zpool", "status -Pj zfsPool", zpoolStatusScanningJson)
            .On("zpool", "status zfsPool", "  scan: scrub in progress\n");
        var service = new ZpoolService(executor);

        var result = await service.GetPoolWithScrubAsync("zfsPool");

        Assert.NotNull(result);
        Assert.Equal("running", result.Value.Scrub.State);
        Assert.Equal("", result.Value.Scrub.TimeLeft);
    }

    [Fact]
    public async Task GetAllPoolsAsync_ShouldEnrichWithUsableUsage()
    {
        var executor = CreateExecutorForPool();
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();
        var pool = pools.Single(p => p.Name == "zfsPool");

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
        var pool = pools.Single(p => p.Name == "zfsPool");

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
        var pool = pools.Single(p => p.Name == "zfsPool");

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
        var pool = pools.Single(p => p.Name == "zfsPool");

        Assert.Equal("raidz1", pool.VdevType);
        Assert.Equal(3, pool.DataDevices.Count);
        Assert.Equal(2, pool.SpecialDevices.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAllPoolsAsync_EmptyListResponse_ShouldReturnEmpty(string response)
    {
        var executor = new FakeCommandExecutor()
            .On("zpool", "list -Hpvj -o name,size,alloc,free,health,frag", response);
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();

        Assert.Empty(pools);
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
        var pool = pools.Single(p => p.Name == "zfsPool");

        Assert.False(pool.Encrypted);
        Assert.Equal("", pool.EncryptionAlgorithm);
    }

    [Fact]
    public async Task GetAllPoolsAsync_LockedKey_ShouldSetKeyLockedTrue()
    {
        var executor = CreateExecutorForPool();
        executor.On("zfs", "get -Hpj used,available,compression,compressratio,dedup,sync,atime,encryption,keystatus zfsPool",
            BuildZfsGetJson("zfsPool", ("encryption", "aes-256-gcm"), ("keystatus", "unavailable")));
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();
        var pool = pools.Single(p => p.Name == "zfsPool");

        Assert.True(pool.Encrypted);
        Assert.True(pool.KeyLocked);
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
        var pool = pools.Single(p => p.Name == "zfsPool");

        Assert.Equal(0UL, pool.UsableUsed);
        Assert.Equal(0UL, pool.UsableAvail);
    }

    [Fact]
    public async Task GetAllPoolsAsync_PartialPropsOutput_ShouldUseDefaults()
    {
        var executor = CreateExecutorForPool();
        executor.On("zfs", "get -Hpj used,available,compression,compressratio,dedup,sync,atime,encryption,keystatus zfsPool",
            BuildZfsGetJson("zfsPool", ("compression", "zstd")));
        var service = new ZpoolService(executor);

        var pools = await service.GetAllPoolsAsync();
        var pool = pools.Single(p => p.Name == "zfsPool");

        Assert.Equal("zstd", pool.Compression);
        Assert.Equal("1.00x", pool.CompRatio); // default
        Assert.Equal("off", pool.Dedup);        // default
    }

    [Fact]
    public async Task GetAllPoolsWithScrubAsync_PartialPropsOutput_ShouldFail()
    {
        var executor = CreateExecutorForPool();
        executor.On("zfs", "get -Hpj used,available,compression,compressratio,dedup,sync,atime,encryption,keystatus zfsPool",
            BuildZfsGetJson("zfsPool", ("compression", "zstd")));
        var service = new ZpoolService(executor);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAllPoolsWithScrubAsync());
    }

    // ── Concurrency ──────────────────────────────────────────────────────

    private sealed class ConcurrentPoolStatusExecutor(
        string listJson,
        string statusJson,
        string ashiftJson,
        string propertiesJson) : ICommandExecutor
    {
        private readonly TaskCompletionSource<bool> bothStatusRequestsStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int statusInvocationCount;

        public int StatusInvocationCount => Volatile.Read(ref this.statusInvocationCount);

        public async Task<string> ExecuteAsync(string command, string arguments, CancellationToken cancellationToken = default)
        {
            if (command == "zpool" && arguments == "list -Hpvj -o name,size,alloc,free,health,frag")
                return listJson;

            if (command == "zpool" && arguments.StartsWith("status -Pj ", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref this.statusInvocationCount) == 2)
                    this.bothStatusRequestsStarted.TrySetResult(true);

                await this.bothStatusRequestsStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
                return statusJson;
            }

            if (command == "zpool" && arguments.StartsWith("get -Hpj ashift ", StringComparison.Ordinal))
                return ashiftJson;

            if (command == "zfs" && arguments.StartsWith("get -Hpj ", StringComparison.Ordinal))
                return propertiesJson;

            return "";
        }
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

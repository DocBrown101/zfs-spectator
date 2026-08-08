namespace Zfs.Tests;

using Zfs.Core.Services.Parser;

public class ZpoolParserTests
{
    [Fact]
    public void ParsePools_ShouldParseBothPools()
    {
        var json = File.ReadAllText("TestData/zpool_list.json");

        var pools = ZpoolParser.ParsePools(json);

        Assert.Equal(2, pools.Count);
        Assert.Contains(pools, p => p.Name == "miniTank");
        Assert.Contains(pools, p => p.Name == "zfsPool");
    }

    [Fact]
    public void ParsePools_ShouldParseMiniTankProperties()
    {
        var json = File.ReadAllText("TestData/zpool_list.json");

        var pools = ZpoolParser.ParsePools(json);
        var pool = pools.Single(p => p.Name == "miniTank");

        Assert.Equal(1992864825344UL, pool.Size);
        Assert.Equal(21095649280UL, pool.Alloc);
        Assert.Equal(1971769176064UL, pool.Free);
        Assert.Equal("ONLINE", pool.Health);
        Assert.Equal(0, pool.Fragmentation);
        Assert.Equal(0UL, pool.SpecialSize);
    }

    [Fact]
    public void ParsePools_ShouldParseZfsPoolProperties()
    {
        var json = File.ReadAllText("TestData/zpool_list.json");

        var pools = ZpoolParser.ParsePools(json);
        var pool = pools.Single(p => p.Name == "zfsPool");

        Assert.Equal(9998683865088UL, pool.Size);
        Assert.Equal(9498245939200UL, pool.Alloc);
        Assert.Equal(500437925888UL, pool.Free);
        Assert.Equal("ONLINE", pool.Health);
        Assert.Equal(0, pool.Fragmentation);
        Assert.Equal(0UL, pool.SpecialSize);
    }

    [Fact]
    public void ParsePools_ShouldReturnEmptyForEmptyInput()
    {
        Assert.Empty(ZpoolParser.ParsePools(""));
        Assert.Empty(ZpoolParser.ParsePools("   "));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"pools\":[]}")]
    [InlineData("{\"pools\":\"text\"}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"text\"")]
    public void ParsePools_ShouldReturnEmptyForInvalidOrNonObjectJson(string json)
    {
        Assert.Empty(ZpoolParser.ParsePools(json));
    }

    [Theory]
    [InlineData("{\"pools\":[]}")]
    [InlineData("{\"pools\":\"text\"}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"text\"")]
    public void ParsePools_RequireOutput_ShouldReportIncompleteDataForInvalidJson(string json)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ZpoolParser.ParsePools(json, requireOutput: true));

        Assert.Equal("zpool list returned incomplete data", exception.Message);
    }

    [Fact]
    public void ParseAshift_ShouldReturnZeroForNonObjectPool()
    {
        Assert.Equal(0, ZpoolParser.ParseAshift("{\"pools\":[]}", "zfsPool"));
        Assert.Equal(0, ZpoolParser.ParseAshift("{\"pools\":{\"zfsPool\":[]}}", "zfsPool"));
    }

    [Fact]
    public void ParseAshift_ShouldReturnValue()
    {
        var json = File.ReadAllText("TestData/zpool_get_ashift.json");

        var ashift = ZpoolParser.ParseAshift(json, "zfsPool");

        Assert.Equal(12, ashift);
    }

    [Fact]
    public void ParseAshift_ShouldReturnZeroForMissingPool()
    {
        var json = File.ReadAllText("TestData/zpool_get_ashift.json");

        Assert.Equal(0, ZpoolParser.ParseAshift(json, "nonexistent"));
    }

    [Fact]
    public void ParsePoolLayout_ShouldParseVdevStructure()
    {
        var json = File.ReadAllText("TestData/zpool_status.json");

        var layout = ZpoolParser.ParsePoolLayout(json, "zfsPool");

        Assert.Equal("raidz1", layout.VdevType);
        Assert.Equal("", layout.Operation);
        Assert.Equal(3, layout.DataDevices.Count);
        Assert.Equal(2, layout.SpecialDevices.Count);
        Assert.Empty(layout.CacheDevices);
        Assert.Empty(layout.LogDevices);
        Assert.Empty(layout.SpareDevices);
        Assert.Equal(0, layout.PoolErrorsRead);
        Assert.Equal(0, layout.PoolErrorsWrite);
        Assert.Equal(0, layout.PoolErrorsChecksum);
    }

    [Fact]
    public void ParsePoolLayout_ShouldParseDeviceDetails()
    {
        var json = File.ReadAllText("TestData/zpool_status.json");

        var layout = ZpoolParser.ParsePoolLayout(json, "zfsPool");

        var firstData = layout.DataDevices[0];
        Assert.Equal("/dev/disk/by-id/wwn-0x50014ee2c06fdd9f-part2", firstData.Path);
        Assert.Equal("raidz1", firstData.VdevType);
        Assert.Equal("ONLINE", firstData.Status);
        Assert.Equal(0, firstData.ErrorsRead);
        Assert.Equal(0, firstData.ErrorsWrite);
        Assert.Equal(0, firstData.ErrorsChecksum);

        var firstSpecial = layout.SpecialDevices[0];
        Assert.Equal("/dev/disk/by-id/nvme-WDC_PC_SN530_SDBPNPZ-256G-1006_205161805086-part1", firstSpecial.Path);
        Assert.Equal("special", firstSpecial.VdevType);
        Assert.Equal("ONLINE", firstSpecial.Status);
    }

    [Fact]
    public void ParseScrubInfo_ShouldParseFinishedScrub()
    {
        var json = File.ReadAllText("TestData/zpool_status.json");

        var scrub = ZpoolParser.ParseScrubInfo(json, "zfsPool");

        Assert.Equal("finished", scrub.State);
        Assert.Equal(0, scrub.Errors);
        Assert.Contains("12:54:52", scrub.StartTime);
        Assert.Contains("17:22:17", scrub.FinishTime);
        Assert.Equal("04:27:25", scrub.Duration);
    }

    [Fact]
    public void ParseScrubInfo_ShouldComputeDurationWithoutTimezone()
    {
        const string json = """
            {
              "output_version": { "command": "zpool status" },
              "pools": {
                "tank": {
                  "name": "tank",
                  "scan_stats": {
                    "function": "SCRUB",
                    "state": "FINISHED",
                    "start_time": "Wed Mar 27 12:54:52 2024",
                    "end_time": "Wed Mar 27 17:22:17 2024",
                    "errors": "0"
                  }
                }
              }
            }
            """;

        var scrub = ZpoolParser.ParseScrubInfo(json, "tank");

        Assert.Equal("finished", scrub.State);
        Assert.Equal("04:27:25", scrub.Duration);
    }

    [Fact]
    public void ParseScrubInfo_ShouldReturnIdleForMissingData()
    {
        var scrub = ZpoolParser.ParseScrubInfo("{}", "nonexistent");

        Assert.Equal("idle", scrub.State);
    }

    [Fact]
    public void ParseScrubInfo_ShouldParseScanningWithProgress()
    {
        var json = File.ReadAllText("TestData/zpool_status_scanning.json");

        var scrub = ZpoolParser.ParseScrubInfo(json, "zfsPool");

        Assert.Equal("running", scrub.State);
        Assert.Equal(0, scrub.Errors);
        Assert.Contains("09:07:04", scrub.StartTime);
        // issued=7.65T / to_examine=8.64T ≈ 88.54%
        Assert.True(scrub.ProgressPct > 88 && scrub.ProgressPct < 89,
            $"Expected ~88.54% but got {scrub.ProgressPct}%");
    }

    [Fact]
    public void ParseScrubInfo_ShouldDetectScrubOperation()
    {
        var json = File.ReadAllText("TestData/zpool_status_scanning.json");

        var layout = ZpoolParser.ParsePoolLayout(json, "zfsPool");

        Assert.Equal("scrubbing", layout.Operation);
    }

    [Fact]
    public void ParseScrubInfo_ShouldParseCanceledScrub()
    {
        const string json = """
            {
              "pools": {
                "tank": {
                  "name": "tank",
                  "scan_stats": {
                    "function": "SCRUB",
                    "state": "CANCELED",
                    "start_time": "Wed Mar 27 12:54:52 2024",
                    "end_time": "Wed Mar 27 13:02:10 2024",
                    "errors": "3"
                  }
                }
              }
            }
            """;

        var scrub = ZpoolParser.ParseScrubInfo(json, "tank");

        Assert.Equal("canceled", scrub.State);
        Assert.Equal(3, scrub.Errors);
        Assert.Equal("00:07:18", scrub.Duration);
    }

    [Fact]
    public void ParseScrubInfo_ShouldParseRunningResilver()
    {
        const string json = """
            {
              "pools": {
                "tank": {
                  "scan_stats": {
                    "function": "RESILVER",
                    "state": "SCANNING",
                    "start_time": "Wed Mar 27 12:54:52 2024",
                    "to_examine": "100G",
                    "issued": "50G",
                    "errors": "0"
                  }
                }
              }
            }
            """;

        var scrub = ZpoolParser.ParseScrubInfo(json, "tank");

        Assert.Equal("running", scrub.State);
        Assert.Equal(50.0, scrub.ProgressPct);
    }

    [Fact]
    public void ParseScrubInfo_ShouldReturnIdleForNonScrubFunction()
    {
        const string json = """
            {
              "pools": {
                "tank": {
                  "scan_stats": {
                    "function": "ZIO_FLUSH",
                    "state": "SCANNING"
                  }
                }
              }
            }
            """;

        Assert.Equal("idle", ZpoolParser.ParseScrubInfo(json, "tank").State);
    }

    [Fact]
    public void ParseScrubInfo_ShouldStripTimezoneFromGermanTimestamp()
    {
        const string json = """
            {
              "pools": {
                "tank": {
                  "scan_stats": {
                    "function": "SCRUB",
                    "state": "FINISHED",
                    "start_time": "Mi 27. Mär 12:54:52 CET 2024",
                    "end_time": "Mi 27. Mär 17:22:17 CET 2024",
                    "errors": "0"
                  }
                }
              }
            }
            """;

        var scrub = ZpoolParser.ParseScrubInfo(json, "tank");

        Assert.Equal("finished", scrub.State);
        Assert.Equal("04:27:25", scrub.Duration);
    }

    [Fact]
    public void ParseScrubInfo_ShouldReturnEmptyDurationWhenEndBeforeStart()
    {
        const string json = """
            {
              "pools": {
                "tank": {
                  "scan_stats": {
                    "function": "SCRUB",
                    "state": "FINISHED",
                    "start_time": "Wed Mar 27 17:22:17 2024",
                    "end_time": "Wed Mar 27 12:54:52 2024",
                    "errors": "0"
                  }
                }
              }
            }
            """;

        var scrub = ZpoolParser.ParseScrubInfo(json, "tank");

        Assert.Equal("finished", scrub.State);
        Assert.Equal("", scrub.Duration);
    }

    [Fact]
    public void ParsePoolLayout_MiniTank_ShouldBeStripeWithTwoDisks()
    {
        var json = File.ReadAllText("TestData/zpool_status.json");

        var layout = ZpoolParser.ParsePoolLayout(json, "miniTank");

        Assert.Equal("stripe", layout.VdevType);
        Assert.Equal("", layout.Operation);
        Assert.Equal(2, layout.DataDevices.Count);
        Assert.Empty(layout.CacheDevices);
        Assert.Empty(layout.LogDevices);
        Assert.Empty(layout.SpareDevices);
        Assert.Empty(layout.SpecialDevices);
    }

    [Fact]
    public void ParsePoolLayout_Mirror_ShouldParseDevicesWithMirrorRole()
    {
        const string json = """
            {
              "pools": {
                "tank": {
                  "name": "tank",
                  "vdevs": {
                    "tank": {
                      "vdev_type": "root",
                      "vdevs": {
                        "mirror-0": {
                          "vdev_type": "mirror",
                          "vdevs": {
                            "sda": {
                              "vdev_type": "disk",
                              "state": "ONLINE",
                              "path": "/dev/sda",
                              "read_errors": "0",
                              "write_errors": "1",
                              "checksum_errors": "0"
                            },
                            "sdb": {
                              "vdev_type": "disk",
                              "state": "DEGRADED",
                              "path": "/dev/sdb",
                              "read_errors": "0",
                              "write_errors": "0",
                              "checksum_errors": "2"
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """;

        var layout = ZpoolParser.ParsePoolLayout(json, "tank");

        Assert.Equal("mirror", layout.VdevType);
        Assert.Equal(2, layout.DataDevices.Count);
        Assert.All(layout.DataDevices, device => Assert.Equal("mirror", device.VdevType));
        Assert.Equal(1, layout.DataDevices[0].ErrorsWrite);
        Assert.Equal("DEGRADED", layout.DataDevices[1].Status);
        Assert.Equal(2, layout.DataDevices[1].ErrorsChecksum);
    }

    [Fact]
    public void ParsePoolLayout_ShouldParseCacheLogAndSpareDevices()
    {
        const string json = """
            {
              "pools": {
                "tank": {
                  "name": "tank",
                  "vdevs": { "tank": { "vdev_type": "root", "vdevs": {} } },
                  "cache": {
                    "cache0": {
                      "vdev_type": "disk",
                      "state": "ONLINE",
                      "name": "/dev/sdc"
                    }
                  },
                  "logs": {
                    "log0": {
                      "vdev_type": "mirror",
                      "vdevs": {
                        "sdd": { "vdev_type": "disk", "state": "ONLINE", "path": "/dev/sdd" },
                        "sde": { "vdev_type": "disk", "state": "ONLINE", "path": "/dev/sde" }
                      }
                    }
                  },
                  "spares": {
                    "spare0": {
                      "vdev_type": "disk",
                      "state": "AVAIL",
                      "path": "/dev/sdf"
                    }
                  }
                }
              }
            }
            """;

        var layout = ZpoolParser.ParsePoolLayout(json, "tank");

        Assert.Equal("stripe", layout.VdevType);
        Assert.Empty(layout.DataDevices);
        Assert.Single(layout.CacheDevices);
        Assert.Equal("/dev/sdc", layout.CacheDevices[0].Path);
        Assert.Equal(2, layout.LogDevices.Count);
        Assert.All(layout.LogDevices, device => Assert.Equal("log", device.VdevType));
        Assert.Single(layout.SpareDevices);
        Assert.Equal("spare", layout.SpareDevices[0].VdevType);
        Assert.Equal("AVAIL", layout.SpareDevices[0].Status);
    }

    [Fact]
    public void ParsePoolLayout_ShouldDetectResilverOperation()
    {
        const string json = """
            {
              "pools": {
                "tank": {
                  "scan_stats": {
                    "function": "RESILVER",
                    "state": "SCANNING"
                  }
                }
              }
            }
            """;

        var layout = ZpoolParser.ParsePoolLayout(json, "tank");

        Assert.Equal("resilvering", layout.Operation);
    }

    [Fact]
    public void ParseScrubInfo_MiniTank_ShouldBeIdle()
    {
        var json = File.ReadAllText("TestData/zpool_status.json");

        var scrub = ZpoolParser.ParseScrubInfo(json, "miniTank");

        Assert.Equal("idle", scrub.State);
    }

    [Fact]
    public void ParseScrubTimeLeft_ShouldExtractTimeToGo()
    {
        var text = """
              pool: zfsPool
             state: ONLINE
              scan: scrub in progress since Mon Mar 23 09:07:04 2026
                    8.64T / 8.64T scanned, 4.70T / 8.64T issued at 488M/s
                    0B repaired, 54.41% done, 02:20:57 to go
            """;

        var timeLeft = ZpoolParser.ParseScrubTimeLeft(text);

        Assert.Equal("02:20:57", timeLeft);
    }

    [Fact]
    public void ParseScrubTimeLeft_ShouldReturnEmptyWhenNoScrub()
    {
        var text = """
              pool: zfsPool
             state: ONLINE
              scan: scrub repaired 0B in 04:27:25 with 0 errors on Wed Mar 27 17:22:17 2024
            """;

        var timeLeft = ZpoolParser.ParseScrubTimeLeft(text);

        Assert.Equal("", timeLeft);
    }
}

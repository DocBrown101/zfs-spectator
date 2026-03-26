using Zfs.Core.Models;
using Zfs.Core.Services.Parser;

namespace Zfs.Core.Services;

public class ZpoolService(ICommandExecutor cmd) : IZpoolService
{
    // ── VDEV delta state ──────────────────────────────────────────────────

    private readonly Lock vdevLock = new();
    private Dictionary<string, VdevCumulativeSnapshot>? prevVdevData;
    private DateTime prevVdevTime;

    // ── Pools ─────────────────────────────────────────────────────────────

    public async Task<List<Pool>> GetAllPoolsAsync()
    {
        var json = await cmd.ExecuteAsync("zpool", "list -Hpj -o name,size,alloc,free,health,frag");
        if (string.IsNullOrWhiteSpace(json)) return [];

        var pools = ZpoolParser.ParsePools(json);
        var result = new List<Pool>();
        foreach (var pool in pools)
            result.Add(await this.EnrichPoolAsync(pool));
        return result;
    }

    public async Task<List<string>> GetPoolNamesAsync()
    {
        var json = await cmd.ExecuteAsync("zpool", "list -Hpj -o name");
        return ZpoolParser.ParsePoolNames(json);
    }

    public async Task<Pool?> GetPoolByNameAsync(string name)
    {
        var json = await cmd.ExecuteAsync("zpool", $"list -Hpj -o name,size,alloc,free,health,frag {name}");
        if (string.IsNullOrWhiteSpace(json)) return null;

        var pools = ZpoolParser.ParsePools(json);
        if (pools.Count == 0) return null;

        return await this.EnrichPoolAsync(pools[0]);
    }

    private async Task<Pool> EnrichPoolAsync(Pool pool)
    {
        var (usableUsed, usableAvail) = await this.GetPoolRootUsageAsync(pool.Name);
        var (compression, compRatio, dedup, sync, atime) = await this.GetPoolRootPropsAsync(pool.Name);
        var (encrypted, keyLocked, algorithm) = await this.GetPoolEncryptionStatusAsync(pool.Name);
        var layout = await this.ParsePoolLayoutAsync(pool.Name);
        var (specialSize, specialAlloc, specialFree) = layout.SpecialDevices.Count > 0
            ? await this.GetSpecialVdevSizeAsync(pool.Name)
            : (0UL, 0UL, 0UL);

        return pool with
        {
            UsableUsed = usableUsed,
            UsableAvail = usableAvail,
            UsableSize = usableUsed + usableAvail,
            Compression = compression,
            CompRatio = compRatio,
            Dedup = dedup,
            Sync = sync,
            Atime = atime,
            Ashift = await this.GetPoolAshiftAsync(pool.Name),
            VdevType = layout.VdevType,
            Operation = layout.Operation,
            Encrypted = encrypted,
            KeyLocked = keyLocked,
            EncryptionAlgorithm = algorithm,
            DataDevices = layout.DataDevices,
            CacheDevices = layout.CacheDevices,
            LogDevices = layout.LogDevices,
            SpareDevices = layout.SpareDevices,
            SpecialDevices = layout.SpecialDevices,
            SpecialSize = specialSize,
            SpecialAlloc = specialAlloc,
            SpecialFree = specialFree,
            ErrorsRead = layout.PoolErrorsRead,
            ErrorsWrite = layout.PoolErrorsWrite,
            ErrorsChecksum = layout.PoolErrorsChecksum,
        };
    }

    private async Task<(ulong Used, ulong Avail)> GetPoolRootUsageAsync(string poolName)
    {
        var output = await cmd.ExecuteAsync("zfs", $"list -Hp -o used,avail {poolName}");
        var f = output.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (f.Length < 2) return (0, 0);
        return (ParseUlong(f[0]), ParseUlong(f[1]));
    }

    private async Task<(string Compression, string CompRatio, string Dedup, string Sync, string Atime)> GetPoolRootPropsAsync(string poolName)
    {
        var output = await cmd.ExecuteAsync("zfs", $"get -Hp compression,compressratio,dedup,sync,atime {poolName}");
        string compression = "lz4", compRatio = "1.00x", dedup = "off", sync = "standard", atime = "off";

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = line.Split('\t');
            if (f.Length < 3) continue;
            switch (f[1])
            {
                case "compression": compression = f[2]; break;
                case "compressratio": compRatio = f[2]; break;
                case "dedup": dedup = f[2]; break;
                case "sync": sync = f[2]; break;
                case "atime": atime = f[2]; break;
            }
        }
        return (compression, compRatio, dedup, sync, atime);
    }

    private async Task<int> GetPoolAshiftAsync(string poolName)
    {
        var json = await cmd.ExecuteAsync("zpool", $"get -Hpj ashift {poolName}");
        return ZpoolParser.ParseAshift(json, poolName);
    }

    private async Task<(bool Encrypted, bool KeyLocked, string Algorithm)> GetPoolEncryptionStatusAsync(string poolName)
    {
        var output = await cmd.ExecuteAsync("zfs", $"get -Hp -o property,value encryption,keystatus {poolName}");
        bool encrypted = false, keyLocked = false;
        var algorithm = "";

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = line.Split('\t');
            if (f.Length < 2) continue;
            switch (f[0])
            {
                case "encryption" when f[1] is not ("off" or "-"):
                    encrypted = true;
                    algorithm = f[1];
                    break;
                case "keystatus":
                    keyLocked = f[1] == "unavailable";
                    break;
            }
        }
        return (encrypted, keyLocked, algorithm);
    }

    // ── Pool Layout ──────────────────────────────────────────────────────

    private async Task<PoolLayout> ParsePoolLayoutAsync(string poolName)
    {
        var json = await cmd.ExecuteAsync("zpool", $"status -Pj {poolName}");
        var layout = ZpoolParser.ParsePoolLayout(json, poolName);

        UpdatePresence(layout.DataDevices);
        UpdatePresence(layout.CacheDevices);
        UpdatePresence(layout.LogDevices);
        UpdatePresence(layout.SpareDevices);
        UpdatePresence(layout.SpecialDevices);

        return layout;
    }

    private static void UpdatePresence(List<PoolDevice> devices)
    {
        for (var i = 0; i < devices.Count; i++)
        {
            var path = devices[i].Path;
            var present = File.Exists(path) ||
                          File.Exists(path.StartsWith('/') ? path : $"/dev/{path}");
            devices[i] = devices[i] with { Present = present };
        }
    }

    // ── Special VDEV Size ────────────────────────────────────────────────

    private async Task<(ulong Size, ulong Alloc, ulong Free)> GetSpecialVdevSizeAsync(string poolName)
    {
        var json = await cmd.ExecuteAsync("zpool", $"list -Hpvj -o name,size,alloc,free {poolName}");
        return ZpoolParser.ParseSpecialVdevSize(json, poolName);
    }

    // ── Scrub ─────────────────────────────────────────────────────────────

    public async Task<ScrubInfo> GetScrubStatusAsync(string poolName)
    {
        var json = await cmd.ExecuteAsync("zpool", $"status -Pj {poolName}");
        var scrub = ZpoolParser.ParseScrubInfo(json, poolName);

        if (scrub.State == "running")
        {
            var text = await cmd.ExecuteAsync("zpool", $"status {poolName}");
            var timeLeft = ZpoolParser.ParseScrubTimeLeft(text);
            if (!string.IsNullOrEmpty(timeLeft))
                scrub = scrub with { TimeLeft = timeLeft };
        }

        return scrub;
    }

    // ── VDEV Data for all pools (from zpool iostat -vlHp) ──────────────

    public async Task<List<PoolLatencyData>> GetAllPoolsVdevDataAsync()
    {
        var output = await cmd.ExecuteAsync("zpool", "iostat -vlHp");
        var pools = ZpoolParser.ParseVdevIostat(output);

        var snapshot = pools.SelectMany(p => p.Devices).ToDictionary(d => d.DevicePath);

        var now = DateTime.UtcNow;
        Dictionary<string, VdevCumulativeSnapshot>? prev;
        double elapsed;
        lock (this.vdevLock)
        {
            prev = this.prevVdevData;
            elapsed = prev != null ? (now - this.prevVdevTime).TotalSeconds : 0;
            this.prevVdevData = snapshot;
            this.prevVdevTime = now;
        }

        return pools.Select(pool => new PoolLatencyData
        {
            PoolName = pool.PoolName,
            Devices = pool.Devices
                .Select(d => ToLatencyInfo(d, prev?.GetValueOrDefault(d.DevicePath), elapsed))
                .ToList(),
        }).ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static VdevLatencyInfo ToLatencyInfo(VdevCumulativeSnapshot d, VdevCumulativeSnapshot? p, double elapsed)
    {
        double readOps = 0, writeOps = 0, readBw = 0, writeBw = 0;
        double readLatMs = 0, writeLatMs = 0, queueDepth = 0, utilPct = 0;

        if (p is not null && elapsed > 0)
        {
            var dReadOps = Math.Max(d.ReadOps - p.ReadOps, 0);
            var dWriteOps = Math.Max(d.WriteOps - p.WriteOps, 0);
            var dTotalR = Math.Max(d.TotalWaitReadNs - p.TotalWaitReadNs, 0);
            var dTotalW = Math.Max(d.TotalWaitWriteNs - p.TotalWaitWriteNs, 0);
            var dDiskR = Math.Max(d.DiskWaitReadNs - p.DiskWaitReadNs, 0);
            var dDiskW = Math.Max(d.DiskWaitWriteNs - p.DiskWaitWriteNs, 0);
            var wallNs = elapsed * 1_000_000_000;

            readOps = dReadOps / elapsed;
            writeOps = dWriteOps / elapsed;
            readBw = Math.Max(d.ReadBytes - p.ReadBytes, 0) / elapsed;
            writeBw = Math.Max(d.WriteBytes - p.WriteBytes, 0) / elapsed;
            readLatMs = dReadOps > 0 ? dTotalR / dReadOps / 1_000_000.0 : 0;
            writeLatMs = dWriteOps > 0 ? dTotalW / dWriteOps / 1_000_000.0 : 0;
            queueDepth = (dTotalR + dTotalW) / wallNs;
            utilPct = Math.Min((dDiskR + dDiskW) / wallNs * 100, 100);
        }

        return new VdevLatencyInfo
        {
            DevicePath = d.DevicePath,
            DeviceName = VdevLatencyInfo.ShortenDeviceName(Path.GetFileName(d.DevicePath)),
            Role = d.Role,
            ReadLatencyMs = Math.Round(readLatMs, 2),
            WriteLatencyMs = Math.Round(writeLatMs, 2),
            ReadOpsPerSec = Math.Round(readOps, 1),
            WriteOpsPerSec = Math.Round(writeOps, 1),
            ReadBytesPerSec = Math.Round(readBw, 1),
            WriteBytesPerSec = Math.Round(writeBw, 1),
            QueueDepth = Math.Round(queueDepth, 2),
            UtilizationPct = Math.Round(utilPct, 1),
        };
    }

    private static ulong ParseUlong(string s) => ulong.TryParse(s.Trim(), out var v) ? v : 0;
}

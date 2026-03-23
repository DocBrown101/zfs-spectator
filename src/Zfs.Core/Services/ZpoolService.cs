using Zfs.Core.Models;
using Zfs.Core.Services.Parser;

namespace Zfs.Core.Services;

public class ZpoolService(ICommandExecutor cmd)
{
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

    // ── I/O Statistics ───────────────────────────────────────────────────

    public async Task<List<PoolIoSnapshot>> GetAllPoolIoSnapshotsAsync()
    {
        var output = await cmd.ExecuteAsync("zpool", "iostat -Hp");
        var result = new List<PoolIoSnapshot>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // Columns: name  alloc  free  read_ops  write_ops  read_bw  write_bw
            var f = line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (f.Length < 7) continue;

            result.Add(new PoolIoSnapshot(
                Name: f[0],
                ReadOps: ParseUlong(f[3]),
                WriteOps: ParseUlong(f[4]),
                ReadBytes: ParseUlong(f[5]),
                WriteBytes: ParseUlong(f[6])));
        }

        return result;
    }

    private static ulong ParseUlong(string s) => ulong.TryParse(s.Trim(), out var v) ? v : 0;
}

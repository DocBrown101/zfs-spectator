namespace Zfs.Core.Services.Parser;

using System.Text.Json;
using Zfs.Core.Models;

public static class ZfsParser
{
    // ── Datasets (from zfs list -Hpj -t filesystem) ─────────────────────

    public static List<Dataset> ParseDatasets(string json, string poolName)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("datasets", out var datasets)) return [];

        var result = new List<Dataset>();
        foreach (var entry in datasets.EnumerateObject())
        {
            var ds = entry.Value;
            if (!ds.TryGetProperty("properties", out var props)) continue;

            var name = JsonHelper.GetString(ds, "name");
            var depth = name.Count(c => c == '/') - poolName.Count(c => c == '/');
            var shortName = name.Split('/').Last();

            var encValue = JsonHelper.GetPropertyString(props, "encryption");
            var encrypted = encValue is not ("off" or "-" or "");
            var keyLocked = JsonHelper.GetPropertyString(props, "keystatus") == "unavailable";

            var comment = JsonHelper.GetPropertyString(props, "zfsnas:comment");
            if (comment == "-") comment = "";

            var compRatio = JsonHelper.GetPropertyString(props, "compressratio");
            if (!compRatio.EndsWith('x')) compRatio += "x";

            result.Add(new Dataset
            {
                Name = name,
                ShortName = shortName,
                Used = JsonHelper.GetPropertyUlong(props, "used"),
                Avail = JsonHelper.GetPropertyUlong(props, "available"),
                Refer = JsonHelper.GetPropertyUlong(props, "referenced"),
                Quota = JsonHelper.GetPropertyUlong(props, "quota"),
                RefQuota = JsonHelper.GetPropertyUlong(props, "refquota"),
                Compression = JsonHelper.GetPropertyString(props, "compression"),
                CompRatio = compRatio,
                RecordSize = JsonHelper.GetPropertyUlong(props, "recordsize"),
                Mountpoint = JsonHelper.GetPropertyString(props, "mountpoint"),
                Sync = JsonHelper.GetPropertyString(props, "sync"),
                Dedup = JsonHelper.GetPropertyString(props, "dedup"),
                CaseSensitivity = JsonHelper.GetPropertyString(props, "casesensitivity"),
                Refreservation = JsonHelper.GetPropertyUlong(props, "refreservation"),
                Comment = comment,
                Depth = depth,
                Encrypted = encrypted,
                KeyLocked = keyLocked,
                EncryptionAlgorithm = encrypted ? encValue : "",
                Mounted = JsonHelper.GetPropertyString(props, "mounted") == "yes",
                CanMount = JsonHelper.GetPropertyString(props, "canmount"),
                UsedBySnapshots = JsonHelper.GetPropertyUlong(props, "usedbysnapshots"),
            });
        }
        return result;
    }

    // ── Snapshots (from zfs list -Hpj -t snapshot) ──────────────────────

    public static List<Snapshot> ParseSnapshots(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("datasets", out var datasets)) return [];

        var result = new List<Snapshot>();
        foreach (var entry in datasets.EnumerateObject())
        {
            var snap = entry.Value;
            if (!snap.TryGetProperty("properties", out var props)) continue;

            var name = JsonHelper.GetString(snap, "name");
            var datasetName = JsonHelper.GetString(snap, "dataset");
            var snapName = JsonHelper.GetString(snap, "snapshot_name");

            if (datasetName.Length == 0 || snapName.Length == 0)
            {
                var atIdx = name.LastIndexOf('@');
                if (atIdx < 0) continue;
                datasetName = name[..atIdx];
                snapName = name[(atIdx + 1)..];
            }

            var creationStr = JsonHelper.GetPropertyString(props, "creation");
            var creation = long.TryParse(creationStr, out var unix)
                ? DateTimeOffset.FromUnixTimeSeconds(unix)
                : DateTimeOffset.MinValue;

            result.Add(new Snapshot
            {
                Name = name,
                DatasetName = datasetName,
                SnapName = snapName,
                Used = JsonHelper.GetPropertyUlong(props, "used"),
                Refer = JsonHelper.GetPropertyUlong(props, "referenced"),
                Creation = creation,
            });
        }
        return result;
    }

    // ── ZVols (from zfs list -Hpj -t volume) ────────────────────────────

    public static List<ZVol> ParseZVols(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("datasets", out var datasets)) return [];

        var result = new List<ZVol>();
        foreach (var entry in datasets.EnumerateObject())
        {
            var vol = entry.Value;
            if (!vol.TryGetProperty("properties", out var props)) continue;

            var name = JsonHelper.GetString(vol, "name");
            var encValue = JsonHelper.GetPropertyString(props, "encryption");

            var comment = JsonHelper.GetPropertyString(props, "zfsnas:comment");
            if (comment == "-") comment = "";

            var compRatio = JsonHelper.GetPropertyString(props, "compressratio");
            if (!compRatio.EndsWith('x')) compRatio += "x";

            result.Add(new ZVol
            {
                Name = name,
                Pool = name.Split('/').First(),
                Size = JsonHelper.GetPropertyUlong(props, "volsize"),
                Used = JsonHelper.GetPropertyUlong(props, "used"),
                Refer = JsonHelper.GetPropertyUlong(props, "referenced"),
                Compression = JsonHelper.GetPropertyString(props, "compression"),
                CompRatio = compRatio,
                Sync = JsonHelper.GetPropertyString(props, "sync"),
                Dedup = JsonHelper.GetPropertyString(props, "dedup"),
                VolBlockSize = JsonHelper.GetPropertyString(props, "volblocksize"),
                Encrypted = encValue is not ("off" or "-" or ""),
                Comment = comment,
                Refreservation = JsonHelper.GetPropertyUlong(props, "refreservation"),
            });
        }
        return result;
    }
}

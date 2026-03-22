namespace Zfs.Core.Services.Parser;

using System.Text.Json;
using Zfs.Core.Models;

public class ZfsParser
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

            var name = GetString(ds, "name");
            var depth = name.Count(c => c == '/') - poolName.Count(c => c == '/');
            var shortName = name.Split('/').Last();

            var encValue = GetPropertyString(props, "encryption");
            var encrypted = encValue is not ("off" or "-" or "");
            var keyLocked = GetPropertyString(props, "keystatus") == "unavailable";

            var comment = GetPropertyString(props, "zfsnas:comment");
            if (comment == "-") comment = "";

            var compRatio = GetPropertyString(props, "compressratio");
            if (!compRatio.EndsWith('x')) compRatio += "x";

            result.Add(new Dataset
            {
                Name = name,
                ShortName = shortName,
                Used = GetPropertyUlong(props, "used"),
                Avail = GetPropertyUlong(props, "available"),
                Refer = GetPropertyUlong(props, "referenced"),
                Quota = GetPropertyUlong(props, "quota"),
                RefQuota = GetPropertyUlong(props, "refquota"),
                Compression = GetPropertyString(props, "compression"),
                CompRatio = compRatio,
                RecordSize = GetPropertyUlong(props, "recordsize"),
                Mountpoint = GetPropertyString(props, "mountpoint"),
                Sync = GetPropertyString(props, "sync"),
                Dedup = GetPropertyString(props, "dedup"),
                CaseSensitivity = GetPropertyString(props, "casesensitivity"),
                Refreservation = GetPropertyUlong(props, "refreservation"),
                Comment = comment,
                Depth = depth,
                Encrypted = encrypted,
                KeyLocked = keyLocked,
                EncryptionAlgorithm = encrypted ? encValue : "",
                Mounted = GetPropertyString(props, "mounted") == "yes",
                CanMount = GetPropertyString(props, "canmount"),
                UsedBySnapshots = GetPropertyUlong(props, "usedbysnapshots"),
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

            var name = GetString(snap, "name");
            var datasetName = GetString(snap, "dataset");
            var snapName = GetString(snap, "snapshot_name");

            if (datasetName.Length == 0 || snapName.Length == 0)
            {
                var atIdx = name.LastIndexOf('@');
                if (atIdx < 0) continue;
                datasetName = name[..atIdx];
                snapName = name[(atIdx + 1)..];
            }

            var creationStr = GetPropertyString(props, "creation");
            var creation = long.TryParse(creationStr, out var unix)
                ? DateTimeOffset.FromUnixTimeSeconds(unix)
                : DateTimeOffset.MinValue;

            result.Add(new Snapshot
            {
                Name = name,
                DatasetName = datasetName,
                SnapName = snapName,
                Used = GetPropertyUlong(props, "used"),
                Refer = GetPropertyUlong(props, "referenced"),
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

            var name = GetString(vol, "name");
            var encValue = GetPropertyString(props, "encryption");

            var comment = GetPropertyString(props, "zfsnas:comment");
            if (comment == "-") comment = "";

            var compRatio = GetPropertyString(props, "compressratio");
            if (!compRatio.EndsWith('x')) compRatio += "x";

            result.Add(new ZVol
            {
                Name = name,
                Pool = name.Split('/').First(),
                Size = GetPropertyUlong(props, "volsize"),
                Used = GetPropertyUlong(props, "used"),
                Refer = GetPropertyUlong(props, "referenced"),
                Compression = GetPropertyString(props, "compression"),
                CompRatio = compRatio,
                Sync = GetPropertyString(props, "sync"),
                Dedup = GetPropertyString(props, "dedup"),
                VolBlockSize = GetPropertyString(props, "volblocksize"),
                Encrypted = encValue is not ("off" or "-" or ""),
                Comment = comment,
                Refreservation = GetPropertyUlong(props, "refreservation"),
            });
        }
        return result;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static ulong GetPropertyUlong(JsonElement properties, string name)
    {
        if (!properties.TryGetProperty(name, out var prop)) return 0;
        if (!prop.TryGetProperty("value", out var val)) return 0;

        var raw = val.GetString();
        if (string.IsNullOrEmpty(raw) || raw == "-") return 0;

        return ulong.TryParse(raw, out var result) ? result : 0;
    }

    private static string GetPropertyString(JsonElement properties, string name)
    {
        if (!properties.TryGetProperty(name, out var prop)) return "";
        if (!prop.TryGetProperty("value", out var val)) return "";
        return val.GetString() ?? "";
    }

    private static string GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var val)) return "";
        return val.GetString() ?? "";
    }
}

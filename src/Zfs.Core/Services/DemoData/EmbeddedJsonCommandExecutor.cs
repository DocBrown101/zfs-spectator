using System.Reflection;
using System.Text.Json.Nodes;
using Zfs.Core;

namespace Zfs.Core.Services.TestData;

/// <summary>
/// Development-only command executor that serves embedded test data instead of
/// running real ZFS commands, so the real services can be used in demo mode.
/// </summary>
public sealed class EmbeddedJsonCommandExecutor : ICommandExecutor
{
    private const string ZfsVersion = "2.3.1";

    private static readonly (string Command, string ArgsContains, string? Resource)[] Responses =
    [
        ("zpool", "list -Hpvj -o name,size,alloc,free,health,frag", "zpool_list.json"),
        ("zpool", "status -Pj", "zpool_status.json"),
        ("zpool", "get -Hpj ashift", "zpool_get_ashift.json"),
        ("zfs", "get -Hpj", "zfs_get_pool_props.json"),
        ("zfs", "list -Hpj -r -t filesystem", "zfs_list_datasets.json"),
        ("zfs", "list -Hpj -r -t snapshot", "zfs_list_snapshots.json"),
        ("zfs", "list -Hpj -t volume", "zfs_list_zvols.json"),
        ("zfs", "version", null),
        ("cat", "arcstats", "arcstats.txt"),
    ];

    public Task<string> ExecuteAsync(string command, string arguments, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var (cmd, argsContains, resource) in Responses)
        {
            if (command != cmd || !arguments.Contains(argsContains, StringComparison.Ordinal)) continue;
            if (resource == null) return Task.FromResult(ZfsVersion);

            var output = ReadEmbeddedJson(resource);
            if (resource is "zfs_list_datasets.json" or "zfs_list_snapshots.json")
                output = FilterByPool(output, arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1]);

            return Task.FromResult(output);
        }

        return Task.FromResult("");
    }

    private static string ReadEmbeddedJson(string fileName)
    {
        var resourceName = $"Zfs.Core.TestData.{fileName}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string FilterByPool(string json, string poolName)
    {
        var root = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidDataException("Embedded ZFS list data has no JSON root object.");
        var datasets = root["datasets"]?.AsObject()
            ?? throw new InvalidDataException("Embedded ZFS list data has no datasets object.");

        foreach (var name in datasets
                     .Where(item => item.Value?["pool"]?.GetValue<string>() != poolName)
                     .Select(item => item.Key)
                     .ToList())
            datasets.Remove(name);

        return root.ToJsonString();
    }
}

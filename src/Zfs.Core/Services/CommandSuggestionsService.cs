using Zfs.Core.Models;

namespace Zfs.Core.Services;

/// <summary>
/// Provides suggestions for ZFS mutation commands that modify system state and therefore require elevated privileges (sudo). 
/// Additionally, where applicable, declarative NixOS configuration equivalents are provided to persist these mutations across system rebuilds.
/// </summary>
public static class CommandSuggestionsService
{
    public static CommandSuggestion SuggestSetProperty(string dataset, string property, string value) => new()
    {
        Description = $"Set {property} to {value} on {dataset}",
        ZfsCommand = $"sudo zfs set {property}={value} {dataset}",
        NixOsConfig = property switch
        {
            "compression" =>
                $"# configuration.nix — set via postCreationHook or systemd oneshot\n" +
                $"systemd.services.\"zfs-set-{dataset.Replace('/', '-')}-compression\" = {{\n" +
                $"  description = \"Set ZFS compression on {dataset}\";\n" +
                $"  after = [ \"zfs-import.target\" ];\n" +
                $"  wantedBy = [ \"zfs.target\" ];\n" +
                $"  serviceConfig.Type = \"oneshot\";\n" +
                $"  script = ''\n" +
                $"    ${{{{{{}}}}/bin/zfs}} set compression={value} {dataset}\n" +
                $"  '';\n" +
                $"}};",
            "atime" =>
                $"# configuration.nix\n" +
                $"fileSystems.\"{dataset}\" = {{\n" +
                $"  options = [ \"noatime\" ];\n" +
                $"}};",
            "quota" =>
                $"# configuration.nix — set quota via systemd oneshot\n" +
                $"systemd.services.\"zfs-quota-{dataset.Replace('/', '-')}\" = {{\n" +
                $"  after = [ \"zfs-import.target\" ];\n" +
                $"  wantedBy = [ \"zfs.target\" ];\n" +
                $"  serviceConfig.Type = \"oneshot\";\n" +
                $"  script = ''zfs set quota={value} {dataset}'';\n" +
                $"}};",
            _ => null,
        },
    };

    public static CommandSuggestion SuggestCreateSnapshot(string dataset) => new()
    {
        Description = $"Create snapshot of {dataset}",
        ZfsCommand = $"sudo zfs snapshot {dataset}@backup-$(date +%Y%m%d-%H%M%S)",
        NixOsConfig =
            $"# configuration.nix — automated snapshots with sanoid\n" +
            $"services.sanoid = {{\n" +
            $"  enable = true;\n" +
            $"  datasets.\"{dataset}\" = {{\n" +
            $"    autosnap = true;\n" +
            $"    hourly = 24;\n" +
            $"    daily = 30;\n" +
            $"    monthly = 6;\n" +
            $"  }};\n" +
            $"}};",
    };

    public static CommandSuggestion SuggestScrub(string poolName) => new()
    {
        Description = $"Start scrub on {poolName}",
        ZfsCommand = $"sudo zpool scrub {poolName}",
        NixOsConfig =
            $"# configuration.nix — periodic scrub (default: weekly)\n" +
            $"services.zfs.autoScrub = {{\n" +
            $"  enable = true;\n" +
            $"  interval = \"weekly\";\n" +
            $"  pools = [ \"{poolName}\" ];\n" +
            $"}};",
    };

    public static CommandSuggestion SuggestRollback(string snapName) => new()
    {
        Description = $"Rollback to {snapName}",
        ZfsCommand = $"sudo zfs rollback -r {snapName}",
    };

    public static CommandSuggestion SuggestDestroySnapshot(string snapName) => new()
    {
        Description = $"Destroy snapshot {snapName}",
        ZfsCommand = $"sudo zfs destroy {snapName}",
    };

    public static CommandSuggestion SuggestPoolExport(string poolName) => new()
    {
        Description = $"Export pool {poolName}",
        ZfsCommand = $"sudo zpool export {poolName}",
    };

    public static CommandSuggestion SuggestPoolImport(string poolName) => new()
    {
        Description = $"Import pool {poolName}",
        ZfsCommand = $"sudo zpool import {poolName}",
        NixOsConfig =
            $"# configuration.nix — auto-import pool\n" +
            $"boot.zfs.extraPools = [ \"{poolName}\" ];",
    };

    public static CommandSuggestion SuggestCreateChildDataset(string parentDataset) => new()
    {
        Description = $"Create child dataset under {parentDataset}",
        ZfsCommand = $"sudo zfs create -o recordsize=1M {parentDataset}/newdataset",
        NixOsConfig =
            $"# configuration.nix — declarative dataset (requires nixos-generators or manual)\n" +
            $"# Datasets are typically created imperatively, then mounted:\n" +
            $"fileSystems.\"/path/to/mount\" = {{\n" +
            $"  device = \"{parentDataset}/newdataset\";\n" +
            $"  fsType = \"zfs\";\n" +
            $"}};",
    };

    public static CommandSuggestion SuggestSetSpecialSmallBlocks(string pool) => new()
    {
        Description = $"Set special_small_blocks on {pool} (for special vdev metadata offloading)",
        ZfsCommand = $"sudo zfs set special_small_blocks=128K {pool}",
    };

    public static CommandSuggestion SuggestDestroyDataset(string dataset) => new()
    {
        Description = $"Destroy dataset {dataset}",
        ZfsCommand = $"sudo zfs destroy {dataset}",
    };

    public static CommandSuggestion SuggestRenameDataset(string dataset) => new()
    {
        Description = $"Rename dataset {dataset}",
        ZfsCommand = $"sudo zfs rename {dataset} {dataset}_renamed",
    };

    public static CommandSuggestion SuggestCreateZVol(string pool) => new()
    {
        Description = $"Create a new ZVol in {pool}",
        ZfsCommand = $"sudo zfs create -V 10G {pool}/zvolname",
    };

    public static CommandSuggestion SuggestCreatePool() => new()
    {
        Description = "Create a new pool (mirror example)",
        ZfsCommand = "sudo zpool create -o ashift=12 newpool mirror /dev/sdX /dev/sdY",
        NixOsConfig =
            "# configuration.nix — NixOS auto-imports pools listed in:\n" +
            "boot.zfs.extraPools = [ \"newpool\" ];\n" +
            "# Create the pool imperatively first, then add it here.",
    };

    public static CommandSuggestion SuggestSendReceive(string snapName, string target)
    {
        var dataset = snapName.Split('@').First();
        return new()
        {
            Description = $"Send snapshot to another pool/host",
            ZfsCommand = $"sudo zfs send {snapName} | sudo zfs receive {target}",
            NixOsConfig =
                $"# configuration.nix — automated replication with syncoid\n" +
                $"services.syncoid = {{\n" +
                $"  enable = true;\n" +
                $"  commands.\"{dataset}\" = {{\n" +
                $"    source = \"{dataset}\";\n" +
                $"    target = \"{target}\";\n" +
                $"  }};\n" +
                $"}};",
        };
    }
}

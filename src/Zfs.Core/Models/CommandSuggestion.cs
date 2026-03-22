namespace Zfs.Core.Models;

/// <summary>
/// Instead of executing mutations, the dashboard shows the user
/// what command they would need to run (zfs CLI or NixOS config).
/// </summary>
public record CommandSuggestion
{
    public required string Description { get; init; }
    public required string ZfsCommand { get; init; }
    public string? NixOsConfig { get; init; }
}

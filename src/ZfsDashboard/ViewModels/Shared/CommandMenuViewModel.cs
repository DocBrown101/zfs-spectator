using Zfs.Core.Models;

namespace ZfsDashboard.ViewModels.Shared;

public sealed record CommandMenuItemViewModel(
    CommandSuggestion Suggestion,
    string Icon,
    bool IsDangerous = false);

public sealed record CommandMenuViewModel(
    string Id,
    IReadOnlyList<CommandMenuItemViewModel> Items,
    string? TriggerLabel = null,
    string TriggerIcon = "bi-terminal")
{
    public const string PoolToken = "{pool}";
    public const string DatasetToken = "{dataset}";
    public const string SnapshotToken = "{snapshot}";

    public bool IsContextMenu => this.TriggerLabel is null;
}

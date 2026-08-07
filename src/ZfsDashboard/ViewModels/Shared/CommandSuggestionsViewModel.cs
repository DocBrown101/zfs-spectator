using Zfs.Core.Models;

namespace ZfsDashboard.ViewModels.Shared;

public sealed record CommandSuggestionsViewModel(
    IReadOnlyList<CommandSuggestion> Suggestions,
    string Title = "Command Reference");

namespace ZfsDashboard.ViewModels.Shared;

public sealed record KeyValueRowViewModel(
    string Label,
    object Value,
    string? ElementId = null,
    bool Last = false);

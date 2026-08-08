namespace ZfsDashboard.ViewModels.Shared;

public sealed record KeyValueRowViewModel(
    string Label,
    object Value,
    string? ElementId = null,
    bool IsVisible = true,
    string? ValueCss = null,
    bool Last = false);

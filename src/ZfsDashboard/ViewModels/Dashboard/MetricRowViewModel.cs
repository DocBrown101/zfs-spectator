namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record MetricRowViewModel(
    string Label,
    string Value,
    string? ElementId = null,
    bool IsVisible = true,
    string? ValueCss = null);

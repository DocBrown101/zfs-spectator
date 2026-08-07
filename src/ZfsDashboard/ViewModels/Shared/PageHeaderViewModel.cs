namespace ZfsDashboard.ViewModels.Shared;

public sealed record PageHeaderBadge(
    string Text,
    string Css,
    string? Icon = null,
    string? ElementId = null);

public sealed record PageHeaderIcon(
    string IconClass,
    string? Tooltip = null);

public sealed record PageHeaderViewModel(
    string Icon,
    string Title,
    string? Subtitle = null,
    PageHeaderIcon? TitleIcon = null,
    PageHeaderBadge? StatusBadge = null,
    PageHeaderBadge? AlertBadge = null,
    PageHeaderBadge? TrailingBadge = null);

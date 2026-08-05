namespace ZfsDashboard.Models;

public sealed record CopyableCommandViewModel(
    string Command,
    bool Compact = false,
    string Tooltip = "Copy to clipboard");

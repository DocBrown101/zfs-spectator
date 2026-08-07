namespace ZfsDashboard.ViewModels.Shared;

public enum CapacityBarSize
{
    Compact,
    Regular,
    Large,
}

public sealed record CapacityBarViewModel(
    double Percentage,
    CapacityBarSize Size = CapacityBarSize.Compact,
    bool ShowPercentage = true,
    bool ShowThreshold = false);

namespace ZfsDashboard.Models;

public enum CapacityBarSize
{
    Compact,
    Regular,
    Large,
}

public enum CapacityBarHealthyColor
{
    Success,
    Info,
}

public sealed record CapacityBarViewModel(
    double Percentage,
    CapacityBarSize Size = CapacityBarSize.Compact,
    bool ShowPercentage = true,
    bool ShowThreshold = false,
    CapacityBarHealthyColor HealthyColor = CapacityBarHealthyColor.Success);

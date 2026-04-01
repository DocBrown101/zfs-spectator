namespace Zfs.Core.Services;

public interface IDiskTemperatureProvider
{
    /// <summary>
    /// Returns the most recently observed disk temperatures.
    /// Keys are physical disk names (e.g. "sda", "nvme0n1"), values are degrees Celsius.
    /// </summary>
    IReadOnlyDictionary<string, int> Temperatures { get; }
}

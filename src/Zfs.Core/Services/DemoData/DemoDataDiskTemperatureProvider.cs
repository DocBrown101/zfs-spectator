using System.Collections.Frozen;

namespace Zfs.Core.Services.TestData;

public class DemoDataDiskTemperatureProvider : IDiskTemperatureProvider
{
    public IReadOnlyDictionary<string, int> Temperatures { get; } = new Dictionary<string, int>
    {
        ["sda"] = 34,
        ["sdb"] = 36,
        ["sdc"] = 38,
        ["sdd"] = 37,
        ["sde"] = 35,
        ["nvme0n1"] = 49,
        ["nvme1n1"] = 51,
    }.ToFrozenDictionary();
}

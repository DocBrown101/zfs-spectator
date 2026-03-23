using System.Reflection;

namespace Zfs.Core.Services.TestData;

internal static class TestDataHelper
{
    internal static string ReadEmbeddedJson(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"Zfs.Core.TestData.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

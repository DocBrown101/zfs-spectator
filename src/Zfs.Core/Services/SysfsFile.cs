namespace Zfs.Core.Services;

internal static class SysfsFile
{
    internal static string? TryReadFirstLine(string path)
    {
        try
        {
            var line = File.ReadLines(path).FirstOrDefault()?.Trim();
            return string.IsNullOrEmpty(line) ? null : line;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

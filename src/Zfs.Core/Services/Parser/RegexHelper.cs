namespace Zfs.Core.Services.Parser;

using System.Text.RegularExpressions;

internal static partial class RegexHelper
{
    [GeneratedRegex(@"\d+\.\d+(?:\.\d+)?")]
    internal static partial Regex ZfsVersionRegex();

    [GeneratedRegex(@"(\d+:\d+:\d+)\s+to go")]
    internal static partial Regex ScrubTimeLeft();
}

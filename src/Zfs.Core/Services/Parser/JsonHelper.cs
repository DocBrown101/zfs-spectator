namespace Zfs.Core.Services.Parser
{
    using System.Text.Json;

    internal static class JsonHelper
    {
        internal static ulong GetPropertyUlong(JsonElement properties, string name)
        {
            if (!properties.TryGetProperty(name, out var prop)) return 0;
            if (!prop.TryGetProperty("value", out var val)) return 0;

            var raw = val.GetString();
            if (string.IsNullOrEmpty(raw) || raw == "-") return 0;

            return ulong.TryParse(raw, out var result) ? result : 0;
        }

        internal static string GetPropertyString(JsonElement properties, string name)
        {
            if (!properties.TryGetProperty(name, out var prop)) return "";
            if (!prop.TryGetProperty("value", out var val)) return "";
            return val.GetString() ?? "";
        }

        internal static int GetPropertyInt(JsonElement properties, string name)
        {
            if (!properties.TryGetProperty(name, out var prop)) return 0;
            if (!prop.TryGetProperty("value", out var val)) return 0;

            var raw = val.GetString();
            if (string.IsNullOrEmpty(raw) || raw == "-") return 0;

            return int.TryParse(raw, out var result) ? result : 0;
        }

        internal static string GetString(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var val)) return "";
            return val.GetString() ?? "";
        }

        internal static long GetLong(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var val)) return 0;
            var raw = val.GetString();
            return long.TryParse(raw, out var result) ? result : 0;
        }

        private static readonly (string Suffix, double Multiplier)[] ByteSuffixes =
        [
            ("E", 1024.0 * 1024 * 1024 * 1024 * 1024 * 1024),
            ("P", 1024.0 * 1024 * 1024 * 1024 * 1024),
            ("T", 1024.0 * 1024 * 1024 * 1024),
            ("G", 1024.0 * 1024 * 1024),
            ("M", 1024.0 * 1024),
            ("K", 1024.0),
            ("B", 1.0),
        ];

        /// <summary>
        /// Parses ZFS human-readable byte strings like "8.64T", "3.01M", "0B" into bytes.
        /// </summary>
        internal static double ParseByteString(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "-") return 0;

            foreach (var (suffix, multiplier) in ByteSuffixes)
            {
                if (!value.EndsWith(suffix)) continue;

                var numPart = value[..^suffix.Length];
                if (numPart.Length == 0) return 0;

                return double.TryParse(numPart, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var num)
                    ? num * multiplier
                    : 0;
            }

            return 0;
        }
    }
}

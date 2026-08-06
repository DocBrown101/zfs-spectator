namespace Zfs.Core.Services.Parser
{
    using System.Text.Json;

    internal static class JsonHelper
    {
        internal static ulong GetPropertyUlong(JsonElement properties, string name)
            => ulong.TryParse(GetPropertyRaw(properties, name), out var result) ? result : 0;

        internal static int GetPropertyInt(JsonElement properties, string name)
            => int.TryParse(GetPropertyRaw(properties, name), out var result) ? result : 0;

        internal static string GetPropertyString(JsonElement properties, string name)
            => GetPropertyString(properties, name, "");

        internal static string GetPropertyString(JsonElement properties, string name, string fallback)
        {
            var raw = GetPropertyRaw(properties, name);
            return string.IsNullOrEmpty(raw) ? fallback : raw;
        }

        internal static bool IsEncryptionEnabled(JsonElement properties)
            => GetPropertyString(properties, "encryption") is not ("off" or "-" or "");

        internal static bool IsKeyLocked(JsonElement properties)
            => GetPropertyString(properties, "keystatus") == "unavailable";

        private static string? GetPropertyRaw(JsonElement properties, string name)
        {
            if (!properties.TryGetProperty(name, out var prop)) return null;
            if (!prop.TryGetProperty("value", out var val)) return null;
            return val.GetString();
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

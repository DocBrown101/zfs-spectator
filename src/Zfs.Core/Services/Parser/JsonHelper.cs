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
    }
}

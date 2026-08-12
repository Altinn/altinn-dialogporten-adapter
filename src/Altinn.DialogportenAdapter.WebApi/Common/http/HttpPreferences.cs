namespace Altinn.DialogportenAdapter.WebApi.Common.http;

public sealed class HttpPreferences() : Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    public static HttpPreferences FromHeader(string? headerValue)
    {
        if (string.IsNullOrEmpty(headerValue)) return new HttpPreferences();

        var map = new HttpPreferences();

        var preferences = headerValue.Split(",").Select(x => x.Trim()).ToArray();

        foreach (var item in preferences)
        {
            var parts = item.Split("=");
            var key = parts.ElementAtOrDefault(0)?.Trim();
            var value = parts.ElementAtOrDefault(1)?.Trim();

            if (string.IsNullOrEmpty(key)) continue;
            if (string.IsNullOrEmpty(value)) continue;

            map[key] = value;
        }

        return map;
    }

    public TimeZoneInfo? GetTimeZoneOrDefault()
    {
        return TryGetValue("timezone", out var timezone)
            ? TimeZoneInfo.FindSystemTimeZoneById(timezone)
            : null;
    }
}

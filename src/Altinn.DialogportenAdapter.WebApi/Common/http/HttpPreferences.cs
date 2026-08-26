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
            var parts = item.Split("=").Select(x => x.Trim()).ToArray();
            var key = parts.ElementAtOrDefault(0);
            var value = parts.ElementAtOrDefault(1);

            if (string.IsNullOrEmpty(key)) continue;
            if (string.IsNullOrEmpty(value)) continue;

            map[key] = value;
        }

        return map;
    }

    /// <summary>
    /// Gets the timezone preference. Returns null if the timezone preference is not parseable.
    /// </summary>
    /// <returns></returns>
    public TimeZoneInfo? GetTimeZoneOrDefault()
    {
        if (!TryGetValue("timezone", out var timezone)) return null;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone.Trim('"'));
        }
        catch
        {
            return null;
        }
    }
}

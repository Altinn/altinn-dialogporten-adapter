namespace Altinn.DialogportenAdapter.EventSimulator.Common;

internal static class QueryStringExtensions
{
    public static QueryString AddIfNotNull(this QueryString queryString, string key, string? value)
    {
        return value is not null ? queryString.Add(key, value) : queryString;
    }
}
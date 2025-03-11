namespace Altinn.DialogportenAdapter.EventSimulator.Common;

internal static class QueryStringExtensions
{
    public static QueryString AddIf(this QueryString queryString, bool predicate, string key, string? value)
    {
        return predicate ? queryString.Add(key, value ?? "null") : queryString;
    }
}
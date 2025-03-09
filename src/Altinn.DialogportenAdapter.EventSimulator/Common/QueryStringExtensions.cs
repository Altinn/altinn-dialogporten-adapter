namespace Altinn.DialogportenAdapter.EventSimulator.Common;

internal static class QueryStringExtensions
{
    public static QueryString AddIfNotNull(this QueryString queryString, string key, string? value)
    {
        return queryString.AddIf(value is not null, key, value!);
    }
    
    public static QueryString AddIf(this QueryString queryString, bool predicate, string key, string value)
    {
        return predicate ? queryString.Add(key, value) : queryString;
    }
}
using System.Globalization;

namespace Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;

public static class TableKeyExtensions
{
    const string DateFormat = "yyyyMMdd";
    public static string ToPartitionKey(this DateOnly date)
        => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static DateOnly ToDateOnly(this string partitionKey)
        => DateOnly.ParseExact(partitionKey, DateFormat, CultureInfo.InvariantCulture);

    // This method is a placeholder for potential future transformation logic for row keys,
    // or to provide a semantic marker for row key usage in the codebase.
    public static string ToRowKey(this string org)
        => org;

}
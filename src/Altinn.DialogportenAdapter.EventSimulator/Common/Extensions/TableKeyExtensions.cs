using System.Globalization;

namespace Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;

public static class TableKeyExtensions
{
    const string DateFormat = "yyyyMMdd";
    public static string ToPartitionKey(this DateOnly date)
        => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static DateOnly ToDateOnly(this string partitionKey)
        => DateOnly.ParseExact(partitionKey, DateFormat, CultureInfo.InvariantCulture);

    public static string ToRowKey(this string org)
        => org;

}
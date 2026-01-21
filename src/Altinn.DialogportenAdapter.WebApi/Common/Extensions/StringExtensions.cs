namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

public static class StringExtensions
{
    public static string Truncate(this string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
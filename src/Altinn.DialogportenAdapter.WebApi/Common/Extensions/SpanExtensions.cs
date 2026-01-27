namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

internal static class SpanExtensions
{
    /// <summary>
    /// Will try to copy the source span to the destination span from offset, and return true if the entire source span was copied.
    /// If the destination span reminder is too small, the source span will be truncated and "..." will be appended to the destination span.
    /// </summary>
    /// <param name="source">The span to copy from.</param>
    /// <param name="destination">The span to copy to.</param>
    /// <param name="offset">The offset in the destination span to start copying to. Will be updated with the new offset after copying.</param>
    /// <returns>True if the entire source span was copied, false otherwise.</returns>
    public static bool TryCopyTo(this ReadOnlySpan<char> source, Span<char> destination, ref int offset)
    {
        const string andMore = "...";
        var remaining = destination.Length - offset;
        if (remaining < source.Length)
        {
            source[..remaining].CopyTo(destination[offset..]);
            andMore.CopyTo(destination[^andMore.Length..]);
            offset = destination.Length;
            return false;
        }

        source.CopyTo(destination[offset..]);
        offset += source.Length;
        return true;
    }

    public static string Truncate(this ReadOnlySpan<char> text, int maxLength)
    {
        if (text.IsEmpty || text.IsWhiteSpace() || text.Length <= maxLength)
        {
            return text.ToString();
        }

        var offset = 0;
        var buffer = maxLength <= 1024 ? stackalloc char[maxLength] : new char[maxLength];
        text.TryCopyTo(buffer, ref offset);
        return buffer.ToString();
    }
}
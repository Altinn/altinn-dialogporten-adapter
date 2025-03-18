namespace Altinn.DialogportenAdapter.WebApi.Common;

internal static class SpanExtensions
{
    public const string AndMore = "...";
    
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
        var remaining = destination.Length - offset;
        if (remaining <= source.Length)
        {
            source[..Math.Max(remaining - AndMore.Length, 0)].CopyTo(destination[offset..]);
            AndMore.CopyTo(destination[^AndMore.Length..]);
            offset = destination.Length;
            return false;
        }

        source.CopyTo(destination[offset..]);
        offset += source.Length;
        return true;
    }
}
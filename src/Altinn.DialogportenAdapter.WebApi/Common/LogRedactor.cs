using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Altinn.DialogportenAdapter.WebApi.Common;

/// <summary>
/// Redacts PII from request/response bodies before they are written to the logs by
/// <see cref="FourHundredLoggingDelegatingHandler"/>.
/// <para>
/// To extend the redaction, add a property name to <see cref="RedactedPropertyNames"/> (the whole value is
/// replaced) or a regex to <see cref="ValuePatterns"/> (matches are masked wherever they appear in a string value).
/// </para>
/// </summary>
internal static partial class LogRedactor
{
    private const string Placeholder = "[REDACTED]";

    /// <summary>
    /// Property names (case-insensitive) whose entire value - string or nested object - is replaced with
    /// <see cref="Placeholder"/>. Used for free-text fields that may contain names or other personal details.
    /// </summary>
    private static readonly FrozenSet<string> RedactedPropertyNames =
        new[] { "actorName", "title", "summary", "senderName", "additionalInfo" }
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Patterns applied to every string value. The URN patterns keep their prefix and mask only the identifier,
    /// so logs still show what kind of value was redacted.
    /// </summary>
    private static readonly (Regex Pattern, string Replacement)[] ValuePatterns =
    [
        (PersonIdentifierUrn(), $"${{1}}{Placeholder}"),
        (LegacySelfIdentifiedUrn(), $"${{1}}{Placeholder}"),
        (DisplayNameUrn(), $"${{1}}{Placeholder}"),
        (BareNorwegianIdentifier(), Placeholder),
    ];

    public static string? Redact(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(content);
        }
        catch (JsonException)
        {
            // Not JSON (e.g. a plain-text error body) - still mask identifier patterns best-effort.
            return RedactString(content);
        }

        if (node is null)
        {
            return content;
        }

        RedactNode(node);
        return node.ToJsonString();
    }

    private static void RedactNode(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                // Snapshot keys first since we mutate the object while iterating.
                foreach (var key in obj.Select(x => x.Key).ToArray())
                {
                    if (RedactedPropertyNames.Contains(key))
                    {
                        obj[key] = Placeholder;
                    }
                    else if (obj[key] is { } child)
                    {
                        RedactNode(child);
                    }
                }
                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is not null)
                    {
                        RedactNode(item);
                    }
                }
                break;
            case JsonValue value when value.TryGetValue<string>(out var str):
                var redacted = RedactString(str);
                if (!ReferenceEquals(redacted, str))
                {
                    value.ReplaceWith(redacted);
                }
                break;
        }
    }

    private static string RedactString(string value)
    {
        var result = value;
        foreach (var (pattern, replacement) in ValuePatterns)
        {
            result = pattern.Replace(result, replacement);
        }

        return result;
    }

    [GeneratedRegex(@"(urn:altinn:person:identifier-no:)[^""\s/]+", RegexOptions.IgnoreCase)]
    private static partial Regex PersonIdentifierUrn();

    [GeneratedRegex(@"(urn:altinn:person:legacy-selfidentified:)[^""\s/]+", RegexOptions.IgnoreCase)]
    private static partial Regex LegacySelfIdentifiedUrn();

    [GeneratedRegex(@"(urn:altinn:displayName:)[^""\s/]+", RegexOptions.IgnoreCase)]
    private static partial Regex DisplayNameUrn();

    [GeneratedRegex(@"\b\d{11}\b")]
    private static partial Regex BareNorwegianIdentifier();
}

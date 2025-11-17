using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

public static class ApplicationTextParser
{
    private const int DefaultMaxLength = 255;
    private const string TruncateSuffix = "...";

    /// <summary>
    /// This will attempt to find a particular key from the application texts for this app. The order of keys are as follows:
    /// 1. Active task for derived status
    /// 2. Active task
    /// 3. Any task for derived status
    /// 4. Any task and any derived status
    /// The keys have the following format (all lowercase): dp.＜content_type>[.＜task＞[.＜derived_status＞]]
    ///
    /// EBNF:
    /// identifier       ::= "dp." content_type ( "." task_part )?
    /// task_part        ::= task ( "." state )?
    /// content_type     ::= "title" | "summary" | "additionalinfo" | "primaryactionlabel" | "deleteactionlabel" | "copyactionlabel"
    /// task             ::= specific_task | "_any_"
    /// specific_task    ::= alphanumeric_with_internal_dash_or_underscore
    /// state            ::= "archivedunconfirmed" | "archivedconfirmed" | "rejected"
    ///                    | "awaitingserviceownerfeedback" | "awaitingconfirmation"
    ///                    | "awaitingsignature" | "awaitingadditionaluserinput"
    ///                    | "awaitinginitialuserinput"
    ///                    | "awaitinginitialuserinputfromprefill"
    /// alphanumeric_with_internal_dash_or_underscore ::= alphanumeric ( internal_char* alphanumeric )
    /// internal_char    ::= alphanumeric | "_" | "-"
    /// alphanumeric     ::= letter | digit
    /// letter           ::= "a".."z" | "A".."Z"
    /// digit            ::= "0".."9"
    ///
    /// </summary>
    /// <example>
    /// dp.title
    /// dp.summary
    /// dp.summary.Task_1
    /// dp.summary.Task_1.archivedunconfirmed
    /// dp.summary._any_.rejected
    /// </example>
    /// <param name="contentType">The requested content type. Should be title, summary, additionalinfo, primaryactionlabel, deleteactionlabel, or copyactionlabel (case-insensitive)</param>
    /// <param name="instance">The app instance</param>
    /// <param name="applicationTexts">The application texts for all languages</param>
    /// <param name="instanceDerivedStatus">The instance derived status</param>
    /// <param name="maxLength">The max length of the field (default: 255)</param>
    /// <returns>A list of localizations (empty if not defined)</returns>
    internal static List<LocalizationDto> GetLocalizationsFromApplicationTexts(
        string contentType,
        Instance instance,
        ApplicationTexts applicationTexts,
        InstanceDerivedStatus instanceDerivedStatus,
        int maxLength = DefaultMaxLength)
    {
        var keysToCheck = new List<string>(4);
        var prefix = $"dp.{contentType.ToLower()}";
        var instanceTask = instance.Process?.CurrentTask?.ElementId;
        var instanceDerivedStatusString = instanceDerivedStatus.ToString().ToLower();
        if (instanceTask is not null)
        {
            keysToCheck.Add($"{prefix}.{instanceTask}.{instanceDerivedStatusString}");
            keysToCheck.Add($"{prefix}.{instanceTask}");
        }
        keysToCheck.Add($"{prefix}._any_.{instanceDerivedStatusString}");
        keysToCheck.Add(prefix);

#if DEBUG
        Console.WriteLine("Keys to check for content type '{0}': {1}", contentType, string.Join(", ", keysToCheck));
#endif

        var localizations = new List<LocalizationDto>();
        foreach (var translation in applicationTexts.Translations)
        {
            if (!LanguageCodes.IsValidTwoLetterLanguageCode(translation.Language))
            {
                continue;
            }

            foreach (var key in keysToCheck)
            {
                if (!translation.Texts.TryGetValue(key, out var textResource))
                {
                    continue;
                }

                if (textResource.Length > maxLength)
                {
                    textResource = TruncateText(textResource, maxLength);
                }

                localizations.Add(new LocalizationDto
                {
                    LanguageCode = translation.Language,
                    Value = textResource
                });
                break;
            }
        }

        return localizations;
    }
    private static string TruncateText(ReadOnlySpan<char> textResource, int maxLength = DefaultMaxLength)
    {
        // Creates the truncated string without any intermediate string allocations
        return string.Create(maxLength, textResource, (span, text) =>
        {
            text[..(maxLength - TruncateSuffix.Length)].CopyTo(span);
            TruncateSuffix.AsSpan().CopyTo(span[^TruncateSuffix.Length..]);
        });

    }
}

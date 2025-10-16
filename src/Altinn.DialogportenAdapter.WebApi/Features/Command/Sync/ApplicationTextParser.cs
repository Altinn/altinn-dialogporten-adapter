using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

public static class ApplicationTextParser
{
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
    /// content_type     ::= "title" | "summary"
    /// task             ::= specific_task | "_any_"
    /// specific_task    ::= alphanumeric_with_internal_dash_or_underscore
    /// state            ::= "archivedunconfirmed" | "archivedconfirmed" | "rejected"
    ///                    | "awaitingserviceownerfeedback" | "awaitingconfirmation"
    ///                    | "awaitingsignature" | "awaitingadditionaluserinput"
    ///                    | "awaitinginitialuserinput"
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
    /// dp.summary._any_.feedback
    /// </example>
    /// <param name="contentType">The requested content type. Should be Title, Summary or AdditionalInfo</param>
    /// <param name="instance">The app instance</param>
    /// <param name="applicationTexts">The application texts for all languages</param>
    /// <param name="instanceDerivedStatus">The instance derived status</param>
    /// <returns>A list of localizations (empty if not defined)</returns>
    internal static List<LocalizationDto> GetLocalizationsFromApplicationTexts(
        string contentType,
        Instance instance,
        ApplicationTexts applicationTexts,
        InstanceDerivedStatus instanceDerivedStatus)
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

        var localizations = new List<LocalizationDto>();
        foreach (var translation in applicationTexts.Translations)
        {
            foreach (var key in keysToCheck)
            {
                if (!translation.Texts.TryGetValue(key, out var textResource))
                {
                    continue;
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
}
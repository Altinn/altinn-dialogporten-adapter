using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

internal sealed class StorageDialogportenDataMerger
{
    private readonly Settings _settings;
    private readonly ActivityDtoTransformer _activityDtoTransformer;

    public StorageDialogportenDataMerger(Settings settings, ActivityDtoTransformer activityDtoTransformer)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _activityDtoTransformer = activityDtoTransformer ?? throw new ArgumentNullException(nameof(activityDtoTransformer));
    }

    public async Task<DialogDto> Merge(Guid dialogId,
        DialogDto? existing,
        Application application,
        ApplicationTexts applicationTexts,
        Instance instance,
        InstanceEventList events,
        bool isMigration)
    {
        var storageDialog = await ToDialogDto(dialogId, instance, application, applicationTexts, events, isMigration);
        if (existing is null)
        {
            return storageDialog;
        }

        existing.VisibleFrom = storageDialog.VisibleFrom;
        existing.DueAt = storageDialog.DueAt;
        existing.ExternalReference = storageDialog.ExternalReference;
        existing.Status = storageDialog.Status;
        existing.IsApiOnly = storageDialog.IsApiOnly;
        existing.Transmissions.Clear();
        existing.Activities = storageDialog.Activities
            .ExceptBy(existing.Activities.Select(x => x.Id), x => x.Id)
            .ToList();
        // TODO: Attachements blir det duplikater av - hvorfor?
        existing.Attachments =
        [
            ..existing.Attachments.ExceptBy(storageDialog.Attachments.Select(x => x.Id), x => x.Id),
            ..storageDialog.Attachments
        ];
        existing.GuiActions = MergeGuiActions(existing.GuiActions, storageDialog.GuiActions);
        return existing;
    }

    private async Task<DialogDto> ToDialogDto(Guid dialogId, Instance instance, Application application, ApplicationTexts applicationTexts, InstanceEventList events, bool isMigration)
    {
        var instanceDerivedStatus = GetInstanceDerivedStatus(instance, events);
        var status = instanceDerivedStatus switch
        {
            InstanceDerivedStatus.ArchivedUnconfirmed => DialogStatus.Sent,
            InstanceDerivedStatus.ArchivedConfirmed => DialogStatus.Completed,
            InstanceDerivedStatus.Rejected => DialogStatus.RequiresAttention,
            InstanceDerivedStatus.AwaitingServiceOwnerFeedback => DialogStatus.Sent,
            InstanceDerivedStatus.AwaitingConfirmation => DialogStatus.InProgress,
            InstanceDerivedStatus.AwaitingSignature => DialogStatus.InProgress,
            InstanceDerivedStatus.AwaitingAdditionalUserInput => DialogStatus.InProgress,
            InstanceDerivedStatus.AwaitingInitialUserInput => DialogStatus.Draft,
            _ => DialogStatus.InProgress
        };

        var systemLabel = instance.Status switch
        {
            { IsArchived: true } when isMigration => SystemLabel.Archive,
            _ => SystemLabel.Default
        };

        // TODO: Ta stilling til applicaiton.hideSettings https://docs.altinn.studio/altinn-studio/reference/configuration/messagebox/hide_instances/
        // TODO: Hva med om Attachments er for lang?
        // TODO: Hva med om Activities er for lang?
        return new DialogDto
        {
            Id = dialogId,
            // TODO: Sett korrekt bool
            IsApiOnly = application.ShouldBeHidden(instance),
            Party = await ToParty(instance.InstanceOwner),
            ServiceResource = ToServiceResource(instance.AppId),
            SystemLabel = systemLabel,
            CreatedAt = instance.Created,
            UpdatedAt = instance.LastChanged,
            VisibleFrom = instance.VisibleAfter > DateTimeOffset.UtcNow ? instance.VisibleAfter : null,
            DueAt = instance.DueBefore > DateTimeOffset.UtcNow ? instance.DueBefore : null,
            ExternalReference = $"urn:altinn:integration:storage:{instance.Id}",
            Status = status,
            Content = new ContentDto
            {
                // TODO: Skal vi bruke non-sensitive title?
                Title = new ContentValueDto
                {
                    MediaType = MediaTypes.PlainText,
                    Value = GetTitle(instance, application, applicationTexts, instanceDerivedStatus)
                },
                Summary = new ContentValueDto
                {
                    MediaType = MediaTypes.PlainText,
                    Value = GetSummary(instance, applicationTexts, instanceDerivedStatus)
                }
            },
            GuiActions =
            [
                CreateGoToAction(dialogId, instance),
                CreateDeleteAction(dialogId, instance),
                ..CreateCopyAction(dialogId, instance, application)
            ],
            Attachments = instance.Data
                .Select(x => new AttachmentDto
                {
                    Id = Guid.Parse(x.Id).ToVersion7(x.Created.Value),
                    DisplayName = [new() {LanguageCode = "nb", Value = x.Filename ?? x.DataType}],
                    Urls = [new()
                    {
                        ConsumerType = x.Filename is not null
                            ? AttachmentUrlConsumerType.Gui
                            : AttachmentUrlConsumerType.Api,
                        MediaType = x.ContentType,
                        Url = x.SelfLinks.Platform
                    }]
                })
                .ToList(),
            Activities = _activityDtoTransformer.GetActivities(events)
        };
    }

    private static InstanceDerivedStatus GetInstanceDerivedStatus(Instance instance, InstanceEventList events) =>
        instance.Process?.CurrentTask?.AltinnTaskType?.ToLower() switch
        {
            // Hvis vi har CompleteConfirmations etter arkivering kan vi regne denne som "ferdig", før det er den bare sent
            _ when instance.Status.IsArchived => instance.CompleteConfirmations.Count != 0
                ? InstanceDerivedStatus.ArchivedConfirmed : InstanceDerivedStatus.ArchivedUnconfirmed,
            "reject" => InstanceDerivedStatus.Rejected,
            "feedback" => InstanceDerivedStatus.AwaitingServiceOwnerFeedback,
            "confirmation" => InstanceDerivedStatus.AwaitingConfirmation,
            "signing" => InstanceDerivedStatus.AwaitingSignature,
            // Hvis vi tidligere har hatt en "feedback" og er nå på en annen task, er vi "InProgress"
            _ when events.InstanceEvents.Any(x => StringComparer.OrdinalIgnoreCase.Equals(x
                .ProcessInfo?
                .CurrentTask?
                .AltinnTaskType, "Feedback")) => InstanceDerivedStatus.AwaitingAdditionalUserInput,
            _ => InstanceDerivedStatus.AwaitingInitialUserInput
        };

    private List<LocalizationDto> GetTitle(Instance instance, Application application, ApplicationTexts applicationTexts, InstanceDerivedStatus instanceDerivedStatus)
    {
        var title = GetLocalizationsFromApplicationTexts(nameof(DialogDto.Content.Title), instance, applicationTexts, instanceDerivedStatus);
        return title.Count > 0 ? title :
            GetTitleFallback(instance, application);
    }

    private static List<LocalizationDto> GetTitleFallback(Instance instance, Application application)
    {
        return application.Title
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => new LocalizationDto
            {
                LanguageCode = x.Key,
                Value = ToTitle(x.Value, instance.PresentationTexts?.Values)
            })
            .ToList();
    }

    private List<LocalizationDto> GetSummary(Instance instance, ApplicationTexts applicationTexts, InstanceDerivedStatus instanceDerivedStatus)
    {
        var summary = GetLocalizationsFromApplicationTexts(nameof(DialogDto.Content.Summary), instance, applicationTexts, instanceDerivedStatus);
        return summary.Count > 0 ? summary :
            GetSummaryFallback(instanceDerivedStatus);
    }

    private static List<LocalizationDto> GetSummaryFallback(InstanceDerivedStatus instanceDerivedStatus) =>
        instanceDerivedStatus switch
        {
            InstanceDerivedStatus.ArchivedUnconfirmed => [
                new() { LanguageCode = "nb", Value = "Innsendingen er maskinelt kontrollert og formidlet, venter på endelig bekreftelse. Du kan åpne dialogen for å se en foreløpig kvittering." },
                new() { LanguageCode = "nn", Value = "Innsendinga er maskinelt kontrollert og formidla, ventar på endeleg stadfesting. Du kan opne dialogen for å sjå ei førebels kvittering." },
                new() { LanguageCode = "en", Value = "The submission has been automatically checked and forwarded, awaiting final confirmation. You can open the dialog to see a preliminary receipt." }
            ],
            InstanceDerivedStatus.ArchivedConfirmed => [
                new() { LanguageCode = "nb", Value = "Innsendingen er bekreftet mottatt. Du kan åpne dialogen for å se din kvittering." },
                new() { LanguageCode = "nn", Value = "Innsendinga er stadfesta motteken. Du kan opne dialogen for å sjå di kvittering." },
                new() { LanguageCode = "en", Value = "The submission has been confirmed as received. You can open the dialog to see your receipt." }
            ],
            InstanceDerivedStatus.Rejected => [
                new() { LanguageCode = "nb", Value = "Innsendingen ble avvist. Åpne dialogen for mer informasjon." },
                new() { LanguageCode = "nn", Value = "Innsendinga vart avvist. Opne dialogen for meir informasjon." },
                new() { LanguageCode = "en", Value = "The submission was rejected. Open the dialog for more information." }
            ],
            InstanceDerivedStatus.AwaitingServiceOwnerFeedback => [
                new() { LanguageCode = "nb", Value = "Innsendingen er maskinelt kontrollert og formidlet, venter på tilbakemelding." },
                new() { LanguageCode = "nn", Value = "Innsendinga er maskinelt kontrollert og formidla, ventar på tilbakemelding." },
                new() { LanguageCode = "en", Value = "The submission has been automatically checked and forwarded, awaiting feedback." }
            ],
            InstanceDerivedStatus.AwaitingConfirmation => [
                new() { LanguageCode = "nb", Value = "Innsendingen må bekreftes for å gå til neste steg." },
                new() { LanguageCode = "nn", Value = "Innsendinga må stadfestast for å gå til neste steg." },
                new() { LanguageCode = "en", Value = "The submission must be confirmed to proceed to the next step." }
            ],
            InstanceDerivedStatus.AwaitingSignature => [
                new() { LanguageCode = "nb", Value = "Innsendingen må signeres for å gå til neste steg." },
                new() { LanguageCode = "nn", Value = "Innsendinga må signerast for å gå til neste steg." },
                new() { LanguageCode = "en", Value = "The submission must be signed to proceed to the next step." }
            ],
            InstanceDerivedStatus.AwaitingAdditionalUserInput => [
                new() { LanguageCode = "nb", Value = "Innsendingen er under arbeid og trenger flere opplysninger for å gå til neste steg." },
                new() { LanguageCode = "nn", Value = "Innsendinga er under arbeid og treng fleire opplysningar for å gå til neste steg." },
                new() { LanguageCode = "en", Value = "The submission is in progress and requires more information to proceed to the next step." }
            ],
            InstanceDerivedStatus.AwaitingInitialUserInput => [
                new() { LanguageCode = "nb", Value = "Innsendingen er klar for å fylles ut." },
                new() { LanguageCode = "nn", Value = "Innsendinga er klar til å fyllast ut." },
                new() { LanguageCode = "en", Value = "The submission is ready to be filled out." }
            ],
            _ => [
                new() { LanguageCode = "nb", Value = "Innsendingen er klar for å fylles ut." },
                new() { LanguageCode = "nn", Value = "Innsendinga er klar til å fyllast ut." },
                new() { LanguageCode = "en", Value = "The submission is ready to be filled out." }
            ]
        };

    /// <summary>
    /// This will attempt to find a particular key from the application texts for this app. The order of keys are as follows:
    /// 1. Active task for derived status
    /// 2. Active task
    /// 3. Any task for derived status
    /// 4. Any task and any derived status
    /// The keys have the following format (all lowercase): dp.＜content_type>[.＜task＞[.＜derived_status＞]]
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
    private List<LocalizationDto> GetLocalizationsFromApplicationTexts(
        string contentType,
        Instance instance,
        ApplicationTexts applicationTexts,
        InstanceDerivedStatus instanceDerivedStatus)
    {
        var keysToCheck = new List<string>(4);
        var prefix = $"dp.{contentType.ToLower()}";
        var instanceTask = instance.Process?.CurrentTask?.AltinnTaskType?.ToLower();
        var instanceDerivedStatusString = instanceDerivedStatus.ToString().ToLower();
        if (instanceTask is null)
        {
            keysToCheck.Add($"{prefix}.{instanceTask}.{instanceDerivedStatusString}");
            keysToCheck.Add($"{prefix}.{instanceTask}");
        }
        keysToCheck.Add($"{prefix}.{instanceDerivedStatusString}");
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
                    Value = textResource // TODO! Check for placeholders for presentation texts
                });
                break;
            }
        }

        return localizations;
    }

    private GuiActionDto CreateGoToAction(Guid dialogId, Instance instance)
    {
        if (instance.Status.IsArchived)
        {
            var platformBaseUri = _settings.DialogportenAdapter.Altinn
                .GetPlatformUri()
                .ToString()
                .TrimEnd('/');
            return new GuiActionDto
            {
                Id = dialogId.CreateDeterministicSubUuidV7("DialogGuiActionGoTo"),
                Action = "read",
                Priority = DialogGuiActionPriority.Primary,
                Title = [
                    new() { LanguageCode = "nb", Value = "Se innsendt skjema" },
                    new() { LanguageCode = "nn", Value = "Sjå innsendt skjema" },
                    new() { LanguageCode = "en", Value = "See submitted form" }
                ],
                Url = $"{platformBaseUri}/receipt/{instance.Id}"
            };
        }

        var appBaseUri = _settings.DialogportenAdapter.Altinn
            .GetAppUriForOrg(instance.Org, instance.AppId)
            .ToString()
            .TrimEnd('/');
        return new GuiActionDto
        {
            Id = dialogId.CreateDeterministicSubUuidV7("DialogGuiActionGoTo"),
            Action = "write",
            AuthorizationAttribute = "urn:altinn:task:" + instance.Process.CurrentTask.ElementId,
            Priority = DialogGuiActionPriority.Primary,
            Title = [
                new() { LanguageCode = "nb", Value = "Gå til skjemautfylling" },
                new() { LanguageCode = "nn", Value = "Gå til skjemautfylling" },
                new() { LanguageCode = "en", Value = "Go to form completion" }
            ],
            Url = $"{appBaseUri}/#/instance/{instance.Id}"
        };
    }

    private GuiActionDto CreateDeleteAction(Guid dialogId, Instance instance)
    {
        var adapterBaseUri = _settings.DialogportenAdapter.Adapter.BaseUri
            .ToString()
            .TrimEnd('/');
        return new GuiActionDto
        {
            Id = dialogId.CreateDeterministicSubUuidV7("DialogGuiActionDelete"),
            Action = "delete",
            Priority = DialogGuiActionPriority.Secondary,
            IsDeleteDialogAction = true,
            Title = [
                new() { LanguageCode = "nb", Value = "Slett" },
                new() { LanguageCode = "nn", Value = "Slett" },
                new() { LanguageCode = "en", Value = "Delete" }
            ],
            Url = $"{adapterBaseUri}/api/v1/instance/{instance.Id}",
            HttpMethod = HttpVerb.DELETE
        };
    }

    private IEnumerable<GuiActionDto> CreateCopyAction(Guid dialogId, Instance instance, Application application)
    {
        var copyEnabled = application.CopyInstanceSettings?.Enabled ?? false;
        if (!instance.Status.IsArchived || !copyEnabled)
        {
            yield break;
        }

        var appBaseUri = _settings.DialogportenAdapter.Altinn
            .GetAppUriForOrg(instance.Org, instance.AppId)
            .ToString()
            .TrimEnd('/');
        yield return new GuiActionDto
        {
            Id = dialogId.CreateDeterministicSubUuidV7("DialogGuiActionCopy"),
            Action = "instantiate",
            Priority = DialogGuiActionPriority.Tertiary,
            Title = [
                new() { LanguageCode = "nb", Value = "Lag ny kopi" },
                new() { LanguageCode = "nn", Value = "Lag ny kopi" },
                new() { LanguageCode = "en", Value = "Create new copy" }
            ],
            Url = $"{appBaseUri}/legacy/instances/{instance.Id}/copy",
            HttpMethod = HttpVerb.GET
        };
    }

    private static async Task<string> ToParty(InstanceOwner instanceOwner)
    {
        return ToPersonIdentifier(instanceOwner.PersonNumber)
            ?? ToOrgIdentifier(instanceOwner.OrganisationNumber)
            ?? await ToFallbackIdentifier(instanceOwner.PartyId)
            ?? throw new ArgumentException("Instance owner must have either a person number or an organisation number");
    }

    private static string? ToPersonIdentifier(string? personNumber)
    {
        const string personPrefix = "urn:altinn:person:identifier-no:";
        return string.IsNullOrWhiteSpace(personNumber) ? null : $"{personPrefix}{personNumber}";
    }

    private static string? ToOrgIdentifier(string? organisationNumber)
    {
        const string orgPrefix = "urn:altinn:organization:identifier-no:";
        return string.IsNullOrWhiteSpace(organisationNumber) ? null : $"{orgPrefix}{organisationNumber}";
    }

    private static Task<string> ToFallbackIdentifier(string? partyId)
    {
        // TODO: we need to lookup the party here
        const string digdir = "urn:altinn:organization:identifier-no:991825827";
        return Task.FromResult(digdir);
    }

    private static string ToServiceResource(ReadOnlySpan<char> appId)
    {
        const string urnPrefix = "urn:altinn:resource:app_";
        Span<char> span = stackalloc char[appId.Length + urnPrefix.Length];
        appId.CopyTo(span[^appId.Length..]);
        span.Replace('/', '_');
        urnPrefix.CopyTo(span);
        return span.ToString();
    }

    private static string ToTitle(string title, IEnumerable<string>? presentationTexts)
    {
        const string separator = ", ";

        List<string> texts =
        [
            title,
            ..presentationTexts?.Where(x => !string.IsNullOrWhiteSpace(x)) ?? []
        ];

        var offset = 0;
        var titleIdealLength =
            texts.Sum(x => x.Length) +
            separator.Length * (texts.Count - 1);
        var titleClampedLength = Math.Clamp(titleIdealLength,
            min: 0,
            max: Constants.DefaultMaxStringLength);

        Span<char> titleSpan = stackalloc char[titleClampedLength];
        using var enumerator = texts.GetEnumerator();
        if (!enumerator.MoveNext() || !enumerator.Current.AsSpan().TryCopyTo(titleSpan, ref offset))
        {
            return titleSpan.ToString();
        }

        while (enumerator.MoveNext())
        {
            if (!separator.AsSpan().TryCopyTo(titleSpan, ref offset)
                || !enumerator.Current.AsSpan().TryCopyTo(titleSpan, ref offset))
            {
                break;
            }
        }

        return titleSpan.ToString();
    }

    /// <summary>
    /// Merge external and internal gui actions by prioritizing external, and attempting to
    /// fill remaining GuiActionPriority with internal. Overflowing internal gui actions
    /// will be discarded.
    /// </summary>
    private static List<GuiActionDto> MergeGuiActions(IEnumerable<GuiActionDto> existingGuiActions, IEnumerable<GuiActionDto> storageGuiActions)
    {
        var storageActions = new Queue<GuiActionDto>(storageGuiActions
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Id));

        if (storageActions.Count == 0)
        {
            return existingGuiActions as List<GuiActionDto> ?? existingGuiActions.ToList();
        }

        var result = existingGuiActions
            .ExceptBy(storageActions.Select(x => x.Id), x => x.Id)
            .ToList();

        var priorityCapacity = Constants.PriorityLimits
            .GroupJoin(result, x => x.Priority, x => x.Priority,
                (priorityLimit, existingActions) =>
                (
                    Priority: priorityLimit.Priority,
                    Capacity: priorityLimit.Limit - existingActions.Count()
                ))
            .Where(x => x.Capacity > 0)
            .OrderBy(x => x.Priority);

        foreach (var (priority, capacity) in priorityCapacity)
        {
            if (storageActions.Count == 0)
            {
                break;
            }

            // We should not promote actions from a lower priority to a higher priority
            if (storageActions.Peek().Priority > priority)
            {
                continue;
            }

            var remaining = capacity;
            while (remaining-- > 0 && storageActions.TryDequeue(out var action))
            {
                action.Priority = priority;
                result.Add(action);
            }
        }

        return result;
    }
}

internal enum InstanceDerivedStatus
{
    ArchivedUnconfirmed = 1,
    ArchivedConfirmed = 2,
    Rejected = 3,
    AwaitingServiceOwnerFeedback = 4,
    AwaitingConfirmation = 5,
    AwaitingSignature = 6,
    AwaitingAdditionalUserInput = 7,
    AwaitingInitialUserInput = 8
}
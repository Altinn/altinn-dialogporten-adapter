using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

internal sealed record MergeDto(
    Guid DialogId,
    DialogDto? ExistingDialog,
    Application Application,
    ApplicationTexts ApplicationTexts,
    Instance Instance,
    InstanceEventList Events,
    bool IsMigration);

internal sealed class StorageDialogportenDataMerger
{
    private readonly Settings _settings;
    private readonly ActivityDtoTransformer _activityDtoTransformer;
    private readonly IRegisterRepository _registerRepository;

    public StorageDialogportenDataMerger(
        Settings settings,
        ActivityDtoTransformer activityDtoTransformer,
        IRegisterRepository registerRepository)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _activityDtoTransformer = activityDtoTransformer ?? throw new ArgumentNullException(nameof(activityDtoTransformer));
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
    }

    public async Task<DialogDto> Merge(MergeDto dto, CancellationToken cancellationToken)
    {
        var existing = dto.ExistingDialog;
        var storageDialog = await ToDialogDto(dto, cancellationToken);
        if (existing is null)
        {
            return storageDialog;
        }

        var syncAdapterSettings = dto.Application.GetSyncAdapterSettings();

        existing.DueAt = syncAdapterSettings.DisableSyncDueAt
            ? existing.DueAt
            : storageDialog.DueAt;

        existing.Status = syncAdapterSettings.DisableSyncStatus
            ? existing.Status
            : storageDialog.Status;

        existing.Content.Title = syncAdapterSettings.DisableSyncContentTitle
            ? existing.Content.Title
            : storageDialog.Content.Title;

        existing.Content.Summary = syncAdapterSettings.DisableSyncContentSummary
            ? existing.Content.Summary
            : storageDialog.Content.Summary;

        existing.Transmissions = syncAdapterSettings.DisableAddTransmissions
            ? []
            : storageDialog.Transmissions
                .ExceptBy(existing.Transmissions.Select(x => x.Id), x => x.Id)
                .ToList();

        existing.Activities = syncAdapterSettings.DisableAddActivities
            ? []
            : storageDialog.Activities
                .ExceptBy(existing.Activities.Select(x => x.Id), x => x.Id)
                .ToList();

        existing.Attachments = syncAdapterSettings.DisableSyncAttachments
            ? existing.Attachments
            : existing.Attachments
                .ExceptBy(storageDialog.Attachments.Select(x => x.Id), x => x.Id)
                .Concat(storageDialog.Attachments)
                .ToList();

        existing.ApiActions = syncAdapterSettings.DisableSyncApiActions
            ? existing.ApiActions
            : existing.ApiActions
                .ExceptBy(storageDialog.ApiActions.Select(x => x.Id), x => x.Id)
                .Concat(storageDialog.ApiActions)
                .ToList();

        existing.GuiActions = syncAdapterSettings.DisableSyncGuiActions
            ? existing.GuiActions
            : MergeGuiActions(dto.DialogId, existing.GuiActions, storageDialog.GuiActions);

        return existing;
    }

    private async Task<DialogDto> ToDialogDto(MergeDto dto, CancellationToken cancellationToken)
    {
        var (instanceDerivedStatus, dialogStatus) = GetStatus(dto.Instance, dto.Events);
        var systemLabel = dto.Instance.Status.IsArchived && dto.IsMigration
            ? SystemLabel.Archive
            : SystemLabel.Default;
        var (party, activities) = await (
            GetPartyUrn(dto.Instance.InstanceOwner.PartyId, cancellationToken),
            _activityDtoTransformer.GetActivities(dto.Events, cancellationToken)
        );

        return new DialogDto
        {
            Id = dto.DialogId,
            IsApiOnly = dto.Application.ShouldBeHidden(dto.Instance),
            Party = party,
            ServiceResource = ToServiceResource(dto.Instance.AppId),
            SystemLabel = systemLabel,
            CreatedAt = dto.Instance.Created,
            UpdatedAt = dto.Instance.LastChanged > dto.Instance.Created
                ? dto.Instance.LastChanged
                : dto.Instance.Created,
            VisibleFrom = dto.Instance.VisibleAfter > DateTimeOffset.UtcNow ? dto.Instance.VisibleAfter : null,
            DueAt = dto.Instance.DueBefore > DateTimeOffset.UtcNow ? dto.Instance.DueBefore : null,
            ServiceOwnerContext = new ServiceOwnerContext
            {
                ServiceOwnerLabels =
                [
                    new ServiceOwnerLabel
                    {
                        Value = $"urn:altinn:integration:storage:{dto.Instance.Id}"
                    }
                ]
            },
            Status = dialogStatus,
            Content = new ContentDto
            {
                Title = new ContentValueDto
                {
                    MediaType = MediaTypes.PlainText,
                    Value = GetTitle(dto.Instance, dto.Application, dto.ApplicationTexts, instanceDerivedStatus)
                },
                Summary = new ContentValueDto
                {
                    MediaType = MediaTypes.PlainText,
                    Value = GetSummary(dto.Instance, dto.ApplicationTexts, instanceDerivedStatus)
                }
            },
            GuiActions =
            [
                CreateGoToAction(dto.DialogId, dto.Instance),
                CreateDeleteAction(dto.DialogId, dto.Instance),
                ..CreateCopyAction(dto.DialogId, dto.Instance, dto.Application)
            ],
            Attachments = dto.Instance.Data
                .Select(x => new AttachmentDto
                {
                    Id = Guid.Parse(x.Id).ToVersion7(x.Created.Value),
                    DisplayName = [new() {LanguageCode = "nb", Value = x.Filename ?? x.DataType}],
                    Urls = [new()
                    {
                        Id = Guid.Parse(x.Id).ToVersion7(x.Created.Value),
                        ConsumerType = x.Filename is not null
                            ? AttachmentUrlConsumerType.Gui
                            : AttachmentUrlConsumerType.Api,
                        MediaType = x.ContentType,
                        Url = x.SelfLinks.Platform
                    }]
                })
                .ToList(),
            Activities = activities
        };
    }

    private async Task<string> GetPartyUrn(string partyId, CancellationToken cancellationToken)
    {
        var response = await _registerRepository.GetActorUrnByPartyId([partyId], cancellationToken);

        if (!response.TryGetValue(partyId, out var actorUrn))
        {
            throw new InvalidOperationException($"Party with id {partyId} not found.");
        }

        return actorUrn.StartsWith(Constants.DisplayNameUrnPrefix)
            ? actorUrn[Constants.DisplayNameUrnPrefix.Length..]
            : actorUrn;
    }

    private static (InstanceDerivedStatus, DialogStatus) GetStatus(Instance instance, InstanceEventList events)
    {
        var instanceDerivedStatus = instance.Process?.CurrentTask?.AltinnTaskType?.ToLower() switch
        {
            _ when instance.Status.IsArchived => (instance.CompleteConfirmations?.Count ?? 0) != 0
                ? InstanceDerivedStatus.ArchivedConfirmed
                : InstanceDerivedStatus.ArchivedUnconfirmed,
            "reject" => InstanceDerivedStatus.Rejected,
            "feedback" => InstanceDerivedStatus.AwaitingServiceOwnerFeedback,
            "confirmation" => InstanceDerivedStatus.AwaitingConfirmation,
            "signing" => InstanceDerivedStatus.AwaitingSignature,
            // If we at some point has had a "feedback" task, we assume that we are now awaiting additional user input
            _ when events.InstanceEvents.Any(x => StringComparer.OrdinalIgnoreCase.Equals(x
                .ProcessInfo?
                .CurrentTask?
                .AltinnTaskType, "Feedback")) => InstanceDerivedStatus.AwaitingAdditionalUserInput,
            // If the instance was created by the service owner (prefill), which is assumed if the first event recorded has an orgId,
            _ when !string.IsNullOrEmpty(events.InstanceEvents.FirstOrDefault()?.User.OrgId) => InstanceDerivedStatus.AwaitingInitialUserInputFromPrefill,
            // if all else fails, assume we are awaiting initial user input (draft)
            _ => InstanceDerivedStatus.AwaitingInitialUserInput
        };

        var dialogStatus = instanceDerivedStatus switch
        {
            InstanceDerivedStatus.ArchivedUnconfirmed => DialogStatus.Awaiting,
            InstanceDerivedStatus.ArchivedConfirmed => DialogStatus.Completed,
            InstanceDerivedStatus.Rejected => DialogStatus.RequiresAttention,
            InstanceDerivedStatus.AwaitingServiceOwnerFeedback => DialogStatus.Awaiting,
            InstanceDerivedStatus.AwaitingConfirmation => DialogStatus.InProgress,
            InstanceDerivedStatus.AwaitingSignature => DialogStatus.InProgress,
            InstanceDerivedStatus.AwaitingAdditionalUserInput => DialogStatus.InProgress,
            InstanceDerivedStatus.AwaitingInitialUserInputFromPrefill => DialogStatus.InProgress,
            InstanceDerivedStatus.AwaitingInitialUserInput => DialogStatus.Draft,
            _ => DialogStatus.InProgress
        };

        return (instanceDerivedStatus, dialogStatus);
    }


    private List<LocalizationDto> GetTitle(Instance instance, Application application, ApplicationTexts applicationTexts, InstanceDerivedStatus instanceDerivedStatus)
    {
        var title = GetLocalizationsFromApplicationTexts(nameof(DialogDto.Content.Title), instance, applicationTexts, instanceDerivedStatus);
        if (title.Count <= 0) return GetTitleFallback(instance, application);
        if (instance.PresentationTexts is null) return title;

        // Apply presentation texts to the title
        foreach (var titleText in title)
        {
            titleText.Value = ToTitle(titleText.Value, instance.PresentationTexts.Values);
        }

        return title;
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
        var instanceTask = instance.Process?.CurrentTask?.ElementId;
        var instanceDerivedStatusString = instanceDerivedStatus.ToString().ToLower();
        if (instanceTask is not null)
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
                    Value = textResource
                });
                break;
            }
        }

        return localizations;
    }

    private GuiActionDto CreateGoToAction(Guid dialogId, Instance instance)
    {
        var goToActionId = dialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.GoTo);
        if (instance.Status.IsArchived)
        {
            var platformBaseUri = _settings.DialogportenAdapter.Altinn
                .GetPlatformUri()
                .ToString()
                .TrimEnd('/');
            return new GuiActionDto
            {
                Id = goToActionId,
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

        // TODO: CurrentTask may be null. What should we do then? (eks instance id 51499006/907c12e2-041a-4275-9d33-67620cdf15b6 tt02)
        var authorizationAttribute = instance.Process?.CurrentTask?.ElementId is not null
            ? "urn:altinn:task:" + instance.Process.CurrentTask.ElementId
            : null;

        return new GuiActionDto
        {
            Id = goToActionId,
            Action = "write",
            AuthorizationAttribute = authorizationAttribute,
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
            Id = dialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.Delete),
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
            Id = dialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.Copy),
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
    private static List<GuiActionDto> MergeGuiActions(Guid dialogId, IEnumerable<GuiActionDto> existingGuiActions, IEnumerable<GuiActionDto> storageGuiActions)
    {
        var storageActions = new Queue<GuiActionDto>(storageGuiActions
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Id));

        if (storageActions.Count == 0)
        {
            return existingGuiActions as List<GuiActionDto> ?? existingGuiActions.ToList();
        }

        var allPotentialInternalKeys = Constants.GuiAction.Keys
            .Select(x => dialogId.CreateDeterministicSubUuidV7(x))
            .ToList();

        var result = existingGuiActions
            .ExceptBy(allPotentialInternalKeys, x => x.Id!.Value)
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
    AwaitingInitialUserInput = 8,
    AwaitingInitialUserInputFromPrefill = 9
}
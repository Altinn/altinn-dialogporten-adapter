using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

internal sealed record MergeDto(
    Guid DialogId,
    DialogDto? ExistingDialog,
    Application Application,
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
        
        // TODO: Replace this when https://github.com/Altinn/dialogporten/issues/1157 is ready
        existing.ExternalReference = storageDialog.ExternalReference;
        existing.Transmissions.Clear();

        var syncAdapterSettings = dto.Application.GetSyncAdapterSettings();
        // Respect syncAdapterSettings.DisableAddTransmissions when dealing with Transmissions
        // Respect syncAdapterSettings.DisableSyncApiActions when dealing with ApiActions
        if (!syncAdapterSettings.DisableSyncDueAt)
        {
            existing.DueAt = storageDialog.DueAt;
        }

        if (!syncAdapterSettings.DisableSyncStatus)
        {
            existing.Status = storageDialog.Status;
        }

        if (!syncAdapterSettings.DisableAddActivities)
        {
            existing.Activities = storageDialog.Activities
                .ExceptBy(existing.Activities.Select(x => x.Id), x => x.Id)
                .ToList();
        }

        if (!syncAdapterSettings.DisableSyncAttachments)
        {
            existing.Attachments =
            [
                ..existing.Attachments.ExceptBy(storageDialog.Attachments.Select(x => x.Id), x => x.Id),
                ..storageDialog.Attachments
            ];
        }

        if (!syncAdapterSettings.DisableSyncGuiActions)
        {
            existing.GuiActions = MergeGuiActions(dto.DialogId, existing.GuiActions, storageDialog.GuiActions);
        }

        if (!syncAdapterSettings.DisableSyncContentSummary)
        {
            existing.Content.Summary = storageDialog.Content.Summary;
        }

        if (!syncAdapterSettings.DisableSyncContentTitle)
        {
            existing.Content.Title = storageDialog.Content.Title;
        }

        return existing;
    }

    private async Task<DialogDto> ToDialogDto(MergeDto dto, CancellationToken cancellationToken)
    {
        var instanceDerivedStatus = GetInstanceDerivedStatus(dto.Instance, dto.Events);
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

        var systemLabel = dto.Instance.Status switch
        {
            { IsArchived: true } when dto.IsMigration => SystemLabel.Archive,
            _ => SystemLabel.Default
        };

        // TODO: Ta stilling til applicaiton.hideSettings https://docs.altinn.studio/altinn-studio/reference/configuration/messagebox/hide_instances/
        // TODO: Hva med om Attachments er for lang?
        // TODO: Hva med om Activities er for lang?
        // TODO: Er instance.InstanceOwner mottaker av meldingen?
        var party = await _registerRepository.GetPartyUrn(dto.Instance.InstanceOwner.PartyId, cancellationToken)
            ?? throw new InvalidOperationException("Party not found.");
        return new DialogDto
        {
            Id = dto.DialogId,
            // TODO: Sett korrekt bool
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
            ExternalReference = $"urn:altinn:integration:storage:{dto.Instance.Id}",
            Status = status,
            Content = new ContentDto
            {
                // TODO: Skal vi bruke non-sensitive title?
                Title = new ContentValueDto
                {
                    MediaType = MediaTypes.PlainText,
                    Value = dto.Application.Title
                        .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                        .Select(x => new LocalizationDto
                        {
                            LanguageCode = x.Key,
                            Value = ToTitle(x.Value, dto.Instance.PresentationTexts?.Values)
                        })
                        .ToList()
                },
                Summary = new ContentValueDto
                {
                    MediaType = MediaTypes.PlainText,
                    Value = await GetSummary(dto.Instance, dto.Application, instanceDerivedStatus)
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
                        ConsumerType = x.Filename is not null
                            ? AttachmentUrlConsumerType.Gui
                            : AttachmentUrlConsumerType.Api,
                        MediaType = x.ContentType,
                        Url = x.SelfLinks.Platform
                    }]
                })
                .ToList(),
            Activities = await _activityDtoTransformer.GetActivities(dto.Events, cancellationToken)
        };
    }

    private static InstanceDerivedStatus GetInstanceDerivedStatus(Instance instance, InstanceEventList events) =>
        instance.Process?.CurrentTask?.AltinnTaskType?.ToLower() switch
        {
            // Hvis vi har CompleteConfirmations etter arkivering kan vi regne denne som "ferdig", før det er den bare sent
            _ when instance.Status.IsArchived => (instance.CompleteConfirmations?.Count ?? 0) != 0
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

    /// <summary>
    /// This method attempts to create a summary for the instance. This will employ the following heuristics:
    /// 1. Check if there is a service owner supplied summary text for the active task for this app
    /// 2. Check if there is a service owner supplied summary text for the app on the given instance status
    /// 3. Check if there is a service owner supplied summary text for the app
    /// 4. Derive a summary from the instance status alone
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="application"></param>
    /// <param name="instanceDerivedStatus"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private async Task<List<LocalizationDto>> GetSummary(Instance instance, Application application, InstanceDerivedStatus instanceDerivedStatus)
    {
        // TODO! Check application texts! See https://github.com/Altinn/dialogporten/issues/2081

        // Step 4: derive a summary from the derived instance status alone
        List<LocalizationDto> summary = instanceDerivedStatus switch
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
            _ => [ // Default case
                new() { LanguageCode = "nb", Value = "Innsendingen er klar for å fylles ut." },
                new() { LanguageCode = "nn", Value = "Innsendinga er klar til å fyllast ut." },
                new() { LanguageCode = "en", Value = "The submission is ready to be filled out." }
            ]
        };

        return await Task.FromResult(summary);
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
        return new GuiActionDto
        {
            Id = goToActionId,
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
    AwaitingInitialUserInput = 8
}
using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;
using Microsoft.Extensions.Options;

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
        IOptionsSnapshot<Settings> settings,
        ActivityDtoTransformer activityDtoTransformer,
        IRegisterRepository registerRepository)
    {
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        _activityDtoTransformer = activityDtoTransformer ?? throw new ArgumentNullException(nameof(activityDtoTransformer));
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
    }

    public async Task<DialogDto> Merge(MergeDto dto, CancellationToken cancellationToken)
    {
        var existing = dto.ExistingDialog.DeepClone();
        var storageDialog = await ToDialogDto(dto, cancellationToken);

        var syncAdapterSettings = dto.Application.GetSyncAdapterSettings();

        if (existing is null)
        {
            storageDialog.DueAt = syncAdapterSettings.DisableSyncDueAt
                ? null
                : storageDialog.DueAt;

            storageDialog.Content.Summary = syncAdapterSettings.DisableSyncContentSummary
                ? null!
                : storageDialog.Content.Summary;

            storageDialog.Activities = syncAdapterSettings.DisableAddActivities
                ? []
                : storageDialog.Activities;

            storageDialog.Attachments = syncAdapterSettings.DisableSyncAttachments
                ? []
                : storageDialog.Attachments;

            storageDialog.Transmissions = syncAdapterSettings.DisableAddTransmissions
                ? []
                : storageDialog.Transmissions;

            storageDialog.Status = syncAdapterSettings.DisableSyncStatus
                ? DialogStatus.NotApplicable
                : storageDialog.Status;

            storageDialog.GuiActions = syncAdapterSettings.DisableSyncGuiActions
                ? null!
                : storageDialog.GuiActions;

            storageDialog.ApiActions = syncAdapterSettings.DisableSyncApiActions
                ? null!
                : storageDialog.ApiActions;

            return storageDialog;
        }

        existing.IsApiOnly = storageDialog.IsApiOnly;

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

        existing.Attachments = syncAdapterSettings.DisableSyncAttachments
            ? existing.Attachments
            : storageDialog.Attachments;

        existing.Transmissions = ApplySourceChangesExceptWhen(
            except: syncAdapterSettings.DisableAddTransmissions,
            destination: existing.Transmissions,
            source: storageDialog.Transmissions,
            keySelector: x => x.Id);

        existing.Activities = ApplySourceChangesExceptWhen(
            except: syncAdapterSettings.DisableAddActivities,
            destination: existing.Activities,
            source: storageDialog.Activities,
            keySelector: x => x.Id);

        existing.ApiActions = ApplySourceChangesExceptWhen(
            except: syncAdapterSettings.DisableSyncApiActions,
            destination: existing.ApiActions,
            source: storageDialog.ApiActions,
            keySelector: x => x.Id);

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
                _activityDtoTransformer.GetActivities(dto.Events, dto.Instance.InstanceOwner, cancellationToken)
            );

        var (attachments, transmissions) = GetAttachmentAndTransmissions(dto, activities);

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
                CreateGoToAction(dto.DialogId, dto.Instance, dto.ApplicationTexts, instanceDerivedStatus),
                CreateDeleteAction(dto.DialogId, dto.Instance, dto.ApplicationTexts, instanceDerivedStatus),
                ..CreateCopyAction(dto.DialogId, dto.Instance, dto.Application, dto.ApplicationTexts, instanceDerivedStatus)
            ],
            Transmissions = transmissions,
            Attachments = attachments,
            Activities = activities
        };

    }

    private (List<AttachmentDto> attachments, List<TransmissionDto> transmissions) GetAttachmentAndTransmissions(
        MergeDto dto,
        List<ActivityDto> activities)
    {
        var appSettings = dto.Application.GetSyncAdapterSettings();
        var data = dto.Instance.Data;
        var attachmentVisibility = ReceiptAttachmentVisibilityDecider.Create(dto.Application);
        if (appSettings.DisableAddTransmissions ||
            !_settings.DialogportenAdapter.Adapter.FeatureFlag.EnableSubmissionTransmissions)
        {
            return (data.Select(d => CreateAttachmentDto(d, attachmentVisibility)).ToList(), []);
        }

        var dataElementQueue = new Queue<DataElement>(data
            .Where(x => !IsPerformedBySo(x))
            .OrderBy(x => x.Created.Value));


        // A2 Instances cant have more than 1 submission
        // so we take all attachments into a single transmission.
        var isA2 = IsA2Instance(dto.Instance);

        var transmissions = activities
            .Where(x => x.Type is DialogActivityType.FormSubmitted)
            .OrderBy(x => x.CreatedAt)
            .Select((a, i) => new TransmissionDto
            {
                Id = a.Id!.Value.ToVersion7(a.CreatedAt!.Value),
                Type = DialogTransmissionType.Submission,
                Sender = a.PerformedBy,
                Content = new TransmissionContentDto
                {
                    Title = new ContentValueDto
                    {
                        Value =
                        [
                            new() { LanguageCode = "nb", Value = $"Innsending #{i + 1}" },
                            new() { LanguageCode = "nn", Value = $"Innsending #{i + 1}" },
                            new() { LanguageCode = "en", Value = $"Submission #{i + 1}" }
                        ],
                        MediaType = "text/plain"
                    }
                },
                Attachments = dataElementQueue
                    .DequeueWhile(e => e.LastChanged <= a.CreatedAt || isA2)
                    .Select(e => CreateTransmissionAttachmentDto(e, attachmentVisibility))
                    .ToList()
            })
            .ToList();

        var attachments = data
            .Where(IsPerformedBySo)
            .Concat(dataElementQueue) // any remaining attachments not already included in transmissions
            .Select(d => CreateAttachmentDto(d, attachmentVisibility))
            .ToList();

        return (attachments, transmissions);
    }

    private AttachmentDto CreateAttachmentDto(DataElement data, ReceiptAttachmentVisibilityDecider attachmentVisibility)
    {
        var consumerType = attachmentVisibility.GetConsumerType(data);
        var platformUrl = data.SelfLinks.Platform;

        var url = consumerType is AttachmentUrlConsumerType.Gui && !string.IsNullOrEmpty(platformUrl)
            ? ToPortalUri(platformUrl)
            : platformUrl;

        return new()
        {
            Id = Guid.Parse(data.Id).ToVersion7(data.Created.Value),
            DisplayName = [new() { LanguageCode = "nb", Value = data.Filename ?? data.DataType }],
            Urls =
            [
                new()
                {
                    Id = Guid.Parse(data.Id).ToVersion7(data.Created.Value),
                    ConsumerType = consumerType,
                    MediaType = data.ContentType,
                    Url = url
                }
            ]
        };
    }

    private TransmissionAttachmentDto CreateTransmissionAttachmentDto(
        DataElement data,
        ReceiptAttachmentVisibilityDecider attachmentVisibility)
    {
        var consumerType = attachmentVisibility.GetConsumerType(data);
        var platformUrl = data.SelfLinks.Platform;

        var url = consumerType is AttachmentUrlConsumerType.Gui && !string.IsNullOrEmpty(platformUrl)
            ? ToPortalUri(platformUrl)
            : platformUrl;

        return new()
        {
            // Ensure unique IDs when the same DataElement appears as both TransmissionAttachment and Attachment
            // This prevents ID collisions that would cause conflicts in Dialogporten
            Id = Guid.CreateVersion7(data.Created!.Value),
            DisplayName = [new() { LanguageCode = "nb", Value = data.Filename ?? data.DataType }],
            Urls =
            [
                new()
                {
                    ConsumerType = consumerType,
                    MediaType = data.ContentType,
                    Url = url
                }
            ]
        };
    }

    private static bool IsPerformedBySo(DataElement data)
    {
        return data.LastChangedBy.Length == 9;
    }

    private async Task<string> GetPartyUrn(string partyId, CancellationToken cancellationToken)
    {
        var response = await _registerRepository.GetActorUrnByPartyId([partyId], cancellationToken);

        if (!response.TryGetValue(partyId, out var actorUrn))
        {
            throw new PartyNotFoundException(partyId);
        }

        return actorUrn.StartsWith(Constants.DisplayNameUrnPrefix)
            ? actorUrn[Constants.DisplayNameUrnPrefix.Length..]
            : actorUrn;
    }

    private static (InstanceDerivedStatus, DialogStatus) GetStatus(Instance instance, InstanceEventList events)
    {
        var instanceDerivedStatus = instance.Process?.CurrentTask?.AltinnTaskType?.ToLower() switch
        {
            _ when instance.Status.IsArchived => IsConsideredConfirmed(instance)
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
    private List<LocalizationDto> GetSummary(Instance instance, ApplicationTexts applicationTexts, InstanceDerivedStatus instanceDerivedStatus)
    {
        var summary = ApplicationTextParser.GetLocalizationsFromApplicationTexts(nameof(DialogDto.Content.Summary), instance, applicationTexts, instanceDerivedStatus);
        return summary.Count > 0
            ? summary
            : GetSummaryFallback(instanceDerivedStatus);
    }

    private static List<LocalizationDto> GetPrimaryAction(Instance instance, ApplicationTexts applicationTexts, InstanceDerivedStatus instanceDerivedStatus)
    {
        var primaryAction = ApplicationTextParser.GetLocalizationsFromApplicationTexts("primaryactionlabel", instance, applicationTexts, instanceDerivedStatus);
        return primaryAction.Count > 0
            ? primaryAction
            : GetPrimaryFallback(instance);
    }

    private static List<LocalizationDto> GetSecondaryAction(Instance instance, ApplicationTexts applicationTexts, InstanceDerivedStatus instanceDerivedStatus)
    {

        var secondaryAction = ApplicationTextParser.GetLocalizationsFromApplicationTexts("secondaryactionlabel", instance, applicationTexts, instanceDerivedStatus);
        return secondaryAction.Count > 0
            ? secondaryAction
            : GetSecondaryFallback();
    }

    private static List<LocalizationDto> GetTertiaryAction(Instance instance, ApplicationTexts applicationTexts, InstanceDerivedStatus instanceDerivedStatus)
    {
        var ternaryAction = ApplicationTextParser.GetLocalizationsFromApplicationTexts("tertiaryactionlabel", instance, applicationTexts, instanceDerivedStatus);
        return ternaryAction.Count > 0
            ? ternaryAction
            : GetTertiaryFallback();
    }
    private static List<LocalizationDto> GetTertiaryFallback()
    {
        return
        [
            new() { LanguageCode = "nb", Value = "Lag ny kopi" },
            new() { LanguageCode = "nn", Value = "Lag ny kopi" },
            new() { LanguageCode = "en", Value = "Create new copy" }
        ];
    }

    private static List<LocalizationDto> GetPrimaryFallback(Instance instance)
    {
        if (instance.Status.IsArchived)
        {
            return
            [
                new() { LanguageCode = "nb", Value = "Se innsendt skjema" },
                new() { LanguageCode = "nn", Value = "Sjå innsendt skjema" },
                new() { LanguageCode = "en", Value = "See submitted form" }
            ];
        }

        return
        [
            new() { LanguageCode = "nb", Value = "Gå til skjemautfylling" },
            new() { LanguageCode = "nn", Value = "Gå til skjemautfylling" },
            new() { LanguageCode = "en", Value = "Go to form completion" }
        ];
    }

    private static List<LocalizationDto> GetSecondaryFallback()
    {
        return
        [
            new() { LanguageCode = "nb", Value = "Slett" },
            new() { LanguageCode = "nn", Value = "Slett" },
            new() { LanguageCode = "en", Value = "Delete" }
        ];
    }

    private static List<LocalizationDto> GetTitle(Instance instance, Application app, ApplicationTexts applicationTexts, InstanceDerivedStatus instanceDerivedStatus)
    {
        var title = ApplicationTextParser.GetLocalizationsFromApplicationTexts(nameof(DialogDto.Content.Title), instance, applicationTexts, instanceDerivedStatus);

        if (title.Count == 0) return GetTitleFallback(instance, app);

        foreach (var localizationDto in title)
        {
            localizationDto.Value = ToTitle(localizationDto.Value, instance.PresentationTexts?.Values);
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

    private static bool IsConsideredConfirmed(Instance instance)
    {
        if (IsA2Instance(instance)) // Archived in Altinn 2, always considered confirmed
        {
            return true;
        }

        return (instance.CompleteConfirmations?.Count ?? 0) != 0;
    }

    private static bool IsA2Instance(Instance instance)
    {
        return instance.DataValues is not null && instance.DataValues.ContainsKey("A2ArchRef");
    }

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
    private static List<LocalizationDto> GetSummaryFallback(InstanceDerivedStatus instanceDerivedStatus)
    {

        // Step 4: derive a summary from the derived instance status alone
        List<LocalizationDto> summary = instanceDerivedStatus switch
        {
            InstanceDerivedStatus.ArchivedUnconfirmed =>
            [
                new() { LanguageCode = "nb", Value = "Innsendingen er maskinelt kontrollert og formidlet, venter på endelig bekreftelse. Du kan åpne dialogen for å se en foreløpig kvittering." },
                new() { LanguageCode = "nn", Value = "Innsendinga er maskinelt kontrollert og formidla, ventar på endeleg stadfesting. Du kan opne dialogen for å sjå ei førebels kvittering." },
                new() { LanguageCode = "en", Value = "The submission has been automatically checked and forwarded, awaiting final confirmation. You can open the dialog to see a preliminary receipt." }
            ],
            InstanceDerivedStatus.ArchivedConfirmed =>
            [
                new() { LanguageCode = "nb", Value = "Innsendingen er bekreftet mottatt. Du kan åpne dialogen for å se din kvittering." },
                new() { LanguageCode = "nn", Value = "Innsendinga er stadfesta motteken. Du kan opne dialogen for å sjå di kvittering." },
                new() { LanguageCode = "en", Value = "The submission has been confirmed as received. You can open the dialog to see your receipt." }
            ],
            InstanceDerivedStatus.Rejected =>
            [
                new() { LanguageCode = "nb", Value = "Innsendingen ble avvist. Åpne dialogen for mer informasjon." },
                new() { LanguageCode = "nn", Value = "Innsendinga vart avvist. Opne dialogen for meir informasjon." },
                new() { LanguageCode = "en", Value = "The submission was rejected. Open the dialog for more information." }
            ],
            InstanceDerivedStatus.AwaitingServiceOwnerFeedback =>
            [
                new() { LanguageCode = "nb", Value = "Innsendingen er maskinelt kontrollert og formidlet, venter på tilbakemelding." },
                new() { LanguageCode = "nn", Value = "Innsendinga er maskinelt kontrollert og formidla, ventar på tilbakemelding." },
                new() { LanguageCode = "en", Value = "The submission has been automatically checked and forwarded, awaiting feedback." }
            ],
            InstanceDerivedStatus.AwaitingConfirmation =>
            [
                new() { LanguageCode = "nb", Value = "Innsendingen må bekreftes for å gå til neste steg." },
                new() { LanguageCode = "nn", Value = "Innsendinga må stadfestast for å gå til neste steg." },
                new() { LanguageCode = "en", Value = "The submission must be confirmed to proceed to the next step." }
            ],
            InstanceDerivedStatus.AwaitingSignature =>
            [
                new() { LanguageCode = "nb", Value = "Innsendingen må signeres for å gå til neste steg." },
                new() { LanguageCode = "nn", Value = "Innsendinga må signerast for å gå til neste steg." },
                new() { LanguageCode = "en", Value = "The submission must be signed to proceed to the next step." }
            ],
            InstanceDerivedStatus.AwaitingAdditionalUserInput =>
            [
                new() { LanguageCode = "nb", Value = "Innsendingen er under arbeid og trenger flere opplysninger for å gå til neste steg." },
                new() { LanguageCode = "nn", Value = "Innsendinga er under arbeid og treng fleire opplysningar for å gå til neste steg." },
                new() { LanguageCode = "en", Value = "The submission is in progress and requires more information to proceed to the next step." }
            ],
            InstanceDerivedStatus.AwaitingInitialUserInput =>
            [
                new() { LanguageCode = "nb", Value = "Innsendingen er klar for å fylles ut." },
                new() { LanguageCode = "nn", Value = "Innsendinga er klar til å fyllast ut." },
                new() { LanguageCode = "en", Value = "The submission is ready to be filled out." }
            ],
            _ =>
            [ // Default case
                new() { LanguageCode = "nb", Value = "Innsendingen er klar for å fylles ut." },
                new() { LanguageCode = "nn", Value = "Innsendinga er klar til å fyllast ut." },
                new() { LanguageCode = "en", Value = "The submission is ready to be filled out." }
            ]
        };
        return summary;

    }

    private GuiActionDto CreateGoToAction(Guid dialogId, Instance instance, ApplicationTexts applicationTexts, InstanceDerivedStatus instanceDerivedStatus)
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
                Title =
                GetPrimaryAction(instance, applicationTexts, instanceDerivedStatus),
                Url = ToPortalUri($"{platformBaseUri}/receipt/{instance.Id}")
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
            Title =
            GetPrimaryAction(instance, applicationTexts, instanceDerivedStatus),
            Url = ToPortalUri($"{appBaseUri}/#/instance/{instance.Id}")
        };
    }

    private GuiActionDto CreateDeleteAction(Guid dialogId, Instance instance, ApplicationTexts applicationTexts, InstanceDerivedStatus instanceDerivedStatus)
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
            Title =
            GetSecondaryAction(instance, applicationTexts, instanceDerivedStatus),
            Url = $"{adapterBaseUri}/api/v1/instance/{instance.Id}",
            HttpMethod = HttpVerb.DELETE
        };
    }

    private IEnumerable<GuiActionDto> CreateCopyAction(Guid dialogId, Instance instance, Application application, ApplicationTexts applicationTexts, InstanceDerivedStatus instanceDerivedStatus)
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
            Title =
            GetTertiaryAction(instance, applicationTexts, instanceDerivedStatus),
            Url = ToPortalUri($"{appBaseUri}/legacy/instances/{instance.Id}/copy"),
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

    private static List<TProperty> ApplySourceChangesExceptWhen<TProperty, TKey>(
        bool except,
        List<TProperty> destination,
        List<TProperty> source,
        Func<TProperty, TKey> keySelector) =>
        except ? destination : destination
            .ExceptBy(source.Select(keySelector), keySelector)
            .Concat(source)
            .ToList();

    /// <summary>
    /// This rewrites links to Altinn 3 apps, so that they go via the authentication endpoint
    /// in Altinn Platform. This ensures that the user session in Altinn 3 is properly initialized/refreshed before being
    /// redirected to the app.
    ///
    /// ie. https://tad.apps.tt02.altinn.no/tad/pagaendesak#/instance/51441547/26cbe3f0-355d-4459-b085-7edaa899b6ba
    /// becomes
    /// https://platform.tt02.altinn.no/authentication/api/v1/authentication?goto=https%3A%2F%2Ftad.apps.tt02.altinn.no%2Ftad%2Fpagaendesak%3FdontChooseReportee%3Dtrue%23%2Finstance%2F51441547%2F26cbe3f0-355d-4459-b085-7edaa899b6ba
    /// </summary>
    /// <param name="instanceUri">A link to an app instance or attachment</param>
    /// <returns>The same link as a "goto" parameter to authentication </returns>
    private string ToPortalUri(string instanceUri)
    {
        ArgumentNullException.ThrowIfNull(instanceUri);

        var authenticationBaseUri =
            _settings.DialogportenAdapter.Altinn.GetPlatformUri().ToString().TrimEnd('/') +
            "/authentication/api/v1/authentication?goto=";

        var hashIndex = instanceUri.IndexOf('#');
        if (hashIndex < 0) hashIndex = instanceUri.Length;
        var separator = instanceUri.AsSpan(0, hashIndex).IndexOf('?') >= 0 ? '&' : '?';
        const string dontChooseReporteeParam = "dontChooseReportee=true";
        var newLen = instanceUri.Length + 1 + dontChooseReporteeParam.Length;

        var gotoUrl = string.Create(newLen, (instanceUri, hashIndex, separator), static (dst, state) =>
        {
            var (src, insertAt, separator) = state;

            // prefix [0..insertAt)
            src.AsSpan(0, insertAt).CopyTo(dst);

            // separator
            dst[insertAt] = separator;

            // param
            dontChooseReporteeParam.AsSpan().CopyTo(dst[(insertAt + 1)..]);

            // suffix [insertAt..end)
            src.AsSpan(insertAt).CopyTo(dst[(insertAt + 1 + dontChooseReporteeParam.Length)..]);
        });

        return string.Concat(authenticationBaseUri, Uri.EscapeDataString(gotoUrl));
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

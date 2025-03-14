using System.Diagnostics;
using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
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
        Instance instance,
        InstanceEventList events)
    {
        var storageDialog = await ToDialogDto(dialogId, instance, application, events);
        if (existing is null)
        {
            return storageDialog;
        }
        
        existing.SystemLabel = storageDialog.SystemLabel;
        existing.VisibleFrom = storageDialog.VisibleFrom;
        existing.DueAt = storageDialog.DueAt;
        existing.ExternalReference = storageDialog.ExternalReference;
        existing.Status = storageDialog.Status;
        existing.Content.Title = storageDialog.Content.Title;
        existing.Content.Summary = storageDialog.Content.Summary;
        existing.Transmissions.Clear();
        existing.Activities = storageDialog.Activities
            .ExceptBy(existing.Activities.Select(x => x.Id), x => x.Id)
            .ToList();
        existing.Attachments =
        [
            ..existing.Attachments.ExceptBy(storageDialog.Attachments.Select(x => x.Id), x => x.Id),
            ..storageDialog.Attachments
        ];
        existing.GuiActions = MergeGuiActions(existing.GuiActions, storageDialog.GuiActions);
        return existing;
    }

    private async Task<DialogDto> ToDialogDto(Guid dialogId, Instance instance, Application application, InstanceEventList events)
    {
        // TODO: Feedback => Sendt (alt før er draft, alt etter er InProgress) må hente fra process history
        var status = instance.Process?.CurrentTask?.AltinnTaskType?.ToLower() switch
        {
            _ when instance.Status.IsArchived => DialogStatus.Completed,
            "reject" => DialogStatus.RequiresAttention,
            "feedback" => DialogStatus.Sent,
            _ when events.InstanceEvents.Any(x => StringComparer.OrdinalIgnoreCase.Equals(x
                .ProcessInfo?
                .CurrentTask?
                .AltinnTaskType, "Feedback")) => DialogStatus.InProgress,
            _ => DialogStatus.Draft // TODO: Hva med gammel ræl drafts? 
        };

        // TODO: Enduser sin soft delete burde være systemlabel.bin, men er serviceowner soft delete??
        var systemLabel = instance.Status switch
        {
            { IsArchived: true } => SystemLabel.Archive,
            _ => SystemLabel.Default
        };
        
        // TODO: Ta stilling til applicaiton.hideSettings https://docs.altinn.studio/altinn-studio/reference/configuration/messagebox/hide_instances/
        // TODO: Ta stilling til create copy (GuiAction for kopier? kun når instansen er arkivert) Spør Storage https://docs.altinn.studio/altinn-studio/reference/configuration/messagebox/create_copy/
        // TODO: Hva med om Attachments er for lang?
        // TODO: Hva med om Activities er for lang? 
        // TODO: Er dato fra storage utc?
        var copyActionEnabled = (application.CopyInstanceSettings?.Enabled ?? false) && instance.Status.IsArchived;

        return new DialogDto
        {
            Id = dialogId,
            Party = await ToParty(instance.InstanceOwner),
            ServiceResource = ToServiceResource(instance.AppId),
            SystemLabel = systemLabel,
            CreatedAt = instance.Created,
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
                    Value = application.Title
                        .Select(x => new LocalizationDto
                        {
                            LanguageCode = x.Key,
                            Value = ToTitle(x.Value, instance.PresentationTexts?.Values)
                        })
                        .ToList()
                },
                Summary = new ContentValueDto
                {
                    MediaType = MediaTypes.PlainText,
                    // TODO: Dette er en midlertidig løsning for å få med all nødvendig informasjon.
                    Value = [new() { LanguageCode = "nb", Value = "Konvertert med DialogportenAdapter..." }]
                }
            },
            GuiActions = [CreateGoToAction(instance, dialogId), CreateDeleteAction(status, instance, dialogId)],
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

    private GuiActionDto CreateGoToAction(Instance instance, Guid dialogId)
    {
        // TODO: Legg inn engelsk og nynorsk
        if (instance.Status.IsArchived)
        {
            var platformBaseUri = _settings.DialogportenAdapter.Altinn.GetPlatformUri();
            return new GuiActionDto
            {
                Id = dialogId.CreateDeterministicSubUuidV7("DialogGuiActionGoTo"),
                Action = "read",
                Priority = DialogGuiActionPriority.Primary,
                Title = [new() { LanguageCode = "nb", Value = "Se innsendt skjema" }],
                Url = $"{platformBaseUri}/receipt/{instance.Id}"
            };
        }

        var appBaseUri = _settings.DialogportenAdapter.Altinn.GetAppUriForOrg(instance.Org);
        return new GuiActionDto
        {
            Id = dialogId.CreateDeterministicSubUuidV7("DialogGuiActionGoTo"),
            Action = "write",
            Priority = DialogGuiActionPriority.Primary,
            Title = [new() { LanguageCode = "nb", Value = "Gå til skjemautfylling" }],
            Url = $"{appBaseUri}/{instance.AppId}/#/instance/{instance.Id}"
        };
    }

    private GuiActionDto CreateDeleteAction(DialogStatus status, Instance instance, Guid dialogId)
    {
        // TODO: Legg inn engelsk og nynorsk
        var adapterBaseUri = _settings.DialogportenAdapter.Adapter.BaseUri;
        var hardDelete = instance.Status.IsSoftDeleted || status is DialogStatus.Draft;
        var (instanceOwner, instanceGuid) = instance.Id.Split('/') switch
        {
            [var party, var id] when 
                int.TryParse(party, out var y)
                && Guid.TryParse(id, out var x) => (y, x),
            _ => throw new UnreachableException()
        };
        return new GuiActionDto
        {
            Id = dialogId.CreateDeterministicSubUuidV7("DialogGuiActionDelete"),
            Action = "delete",
            Priority = DialogGuiActionPriority.Secondary,
            IsDeleteDialogAction = true,
            Title = [new() { LanguageCode = "nb", Value = "Slett skjema" }],
            Url = $"{adapterBaseUri}/api/v1/instance/{instanceOwner}/{instanceGuid}?hard={hardDelete}",
            HttpMethod = HttpVerb.DELETE
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

    private static string ToTitle(ReadOnlySpan<char> title, IReadOnlyCollection<string>? presentationTexts)
    {
        const string separator = ", ";
        
        var offset = 0;
        presentationTexts ??= Array.Empty<string>();
        var titleLength = Math.Min(Constants.DefaultMaxStringLength,
            title.Length
            + presentationTexts.Sum(x => x.Length) 
            + presentationTexts.Count * separator.Length);
        Span<char> titleSpan = stackalloc char[titleLength];
        
        if (!title.TryCopyTo(titleSpan, ref offset))
        {
            return titleSpan.ToString();
        }
        
        foreach (var text in presentationTexts)
        {
            if (!separator.AsSpan().TryCopyTo(titleSpan, ref offset)
                || !text.AsSpan().TryCopyTo(titleSpan, ref offset))
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
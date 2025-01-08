using System.Diagnostics.CodeAnalysis;
using Altinn.Platform.DialogportenAdapter.WebApi.Common;
using Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.Platform.Storage.Interface.Enums;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.Platform.DialogportenAdapter.WebApi.Features.Command.Sync;

internal sealed class StorageDialogportenDataMerger
{
    private readonly Settings _settings;

    public StorageDialogportenDataMerger(Settings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public DialogDto Merge(Guid dialogId,
        DialogDto? existing,
        Application application,
        Instance instance,
        InstanceEventList events)
    {
        var storageDialog = ToDialogDto(dialogId, instance, application, events);
        if (existing is null)
        {
            return storageDialog;
        }
        
        // TODO: Merge the existing dialog with the storage dialog
        storageDialog.Revision = existing.Revision;
        storageDialog.Activities = storageDialog.Activities
            .ExceptBy(existing.Activities.Select(x => x.Id), x => x.Id)
            .ToList();
        
        foreach (var guiAction in storageDialog.GuiActions)
        {
            guiAction.Id = existing.GuiActions
                .Where(x => x.HttpMethod == guiAction.HttpMethod && x.Url == guiAction.Url)
                .Select(x => x.Id)
                .FirstOrDefault();
        }
        
        return storageDialog;
    }
    
    private DialogDto ToDialogDto(Guid dialogId, Instance instance, Application application, InstanceEventList events)
    {
        var lala = events.InstanceEvents
            .OrderBy(x => x.Created)
            .Select(x =>
            {
                if (!Enum.TryParse<InstanceEventType>(x.EventType, ignoreCase: true, out var eventType))
                {
                    return null;
                }

                var activityType = eventType switch
                {
                    InstanceEventType.Created when x.DataId is null => (DialogActivityType?) DialogActivityType.DialogCreated,
                    InstanceEventType.Saved => DialogActivityType.Information, // TODO: Ta eldste - her må mer massering til
                    InstanceEventType.Submited => DialogActivityType.Information,
                    InstanceEventType.Deleted => DialogActivityType.DialogDeleted,
                    InstanceEventType.Undeleted => DialogActivityType.DialogRestored, // TODO: Må implementeres i dialogporten
                    InstanceEventType.Signed => DialogActivityType.SignatureProvided,
                    InstanceEventType.MessageArchived => DialogActivityType.DialogClosed,
                    InstanceEventType.MessageRead => DialogActivityType.DialogOpened,
                    
                    // Får typer for disse i diaogporten
                    InstanceEventType.SentToSign => DialogActivityType.Information,
                    InstanceEventType.SentToPayment => DialogActivityType.Information,
                    InstanceEventType.SentToSendIn => DialogActivityType.Information,
                    InstanceEventType.SentToFormFill => DialogActivityType.Information,
                    _ => null
                    
                    // InstanceEventType.InstanceForwarded => DialogActivityType.Information,
                    // InstanceEventType.InstanceRightRevoked => DialogActivityType.Information,
                    // InstanceEventType.None => expr,
                    // InstanceEventType.ConfirmedComplete => DialogActivityType.DialogDeleted,
                    // InstanceEventType.SubstatusUpdated => expr,
                    // InstanceEventType.NotificationSentSms => expr,
                };
                return !activityType.HasValue ? null
                    : new ActivityDto
                    {
                        Id = x.Id.Value.ToVersion7(x.Created.Value),
                        Type = activityType.Value,
                        CreatedAt = x.Created,
                        PerformedBy = string.IsNullOrWhiteSpace(x.User.OrgId) 
                            ? new() { ActorType = ActorType.ServiceOwner }
                            : new()
                            {
                                ActorType = ActorType.PartyRepresentative, 
                                ActorId = ToPersonIdentifier(x.User.NationalIdentityNumber) 
                                          ?? throw new InvalidOperationException()  
                            }
                    };
            })
            .Where(x => x is not null)
            .Cast<ActivityDto>()
            .ToList();

        var status = instance.Process?.CurrentTask?.AltinnTaskType?.ToLower() switch
        {
            _ when instance.Status.IsArchived => DialogStatus.Completed,
            "reject" => DialogStatus.RequiresAttention,
            "feedback" => DialogStatus.Sent,
            _ when events.InstanceEvents.Any(x => StringComparer.OrdinalIgnoreCase.Equals(x
                .ProcessInfo?
                .CurrentTask?
                .AltinnTaskType, "Feedback")) => DialogStatus.InProgress,
            _ => DialogStatus.Draft
        };

        var systemLabel = instance.Status switch
        {
            // { IsSoftDeleted: true } => SystemLabel.Bin,
            { IsArchived: true } => SystemLabel.Archive,
            _ => SystemLabel.Default
        };
        
        var dialog = new DialogDto
        {
            Id = dialogId,
            Party = ToParty(instance.InstanceOwner),
            ServiceResource = ToServiceResource(instance.AppId),
            SystemLabel = systemLabel,
            CreatedAt = instance.Created,
            VisibleFrom = instance.VisibleAfter > DateTimeOffset.UtcNow ? instance.VisibleAfter : null,
            DueAt = instance.DueBefore < DateTimeOffset.UtcNow ? instance.DueBefore : null,
            ExternalReference = $"{instance.Id}",
            Status = status,
            Content = new ContentDto
            {
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
            GuiActions = [CreateGoToAction(instance), CreateDeleteAction(status, instance)],
            Attachments = instance.Data
                .Select(x => new AttachmentDto
                {
                    // TODO: Add Id to Attachment in dialogporten
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
                .ToList()
        };

        if (TryGetCreatedActivity(events, out var createdActivity))
        {
            dialog.Activities.Add(createdActivity);
        }

        return dialog;
    }
    
    private GuiActionDto CreateGoToAction(Instance instance)
    {
        if (instance.Status.IsArchived)
        {
            var platformBaseUri = _settings.Infrastructure.Altinn.PlatformBaseUri;
            return new GuiActionDto
            {
                Action = "read",
                Priority = DialogGuiActionPriority.Primary,
                Title = [new() { LanguageCode = "nb", Value = "Se innsendt skjema" }],
                Url = $"{platformBaseUri}/receipt/{instance.Id}"
            };
        }

        var appBaseUri = _settings.Infrastructure.Altinn.GetAppUriForOrg(instance.Org);
        return new GuiActionDto
        {
            Action = "write",
            Priority = DialogGuiActionPriority.Primary,
            Title = [new() { LanguageCode = "nb", Value = "Gå til skjemautfylling" }],
            Url = $"{appBaseUri}/{instance.AppId}/#/instance/{instance.Id}"
        };
    }

    private GuiActionDto CreateDeleteAction(DialogStatus status, Instance instance)
    {
        var adapterBaseUri = _settings.Infrastructure.Adapter.BaseUri;
        var hardDelete = instance.Status.IsSoftDeleted || status is DialogStatus.Draft;
        return new GuiActionDto
        {
            Action = "delete",
            Priority = DialogGuiActionPriority.Secondary,
            IsDeleteDialogAction = true,
            Title = [new() { LanguageCode = "nb", Value = "Slett skjema" }],
            Prompt = hardDelete
                ? [new() { LanguageCode = "nb", Value = "Skjemaet blir permanent slettet" }]
                : null,
            Url = $"{adapterBaseUri}/api/v1/instance/{Uri.EscapeDataString(instance.Id)}?hard={hardDelete}",
            HttpMethod = HttpVerb.DELETE
        };
    }

    private static bool TryGetCreatedActivity(InstanceEventList events, [NotNullWhen(true)] out ActivityDto? createdActivity)
    {
        var nationalIdentityNumberByUserId = events.InstanceEvents
            .Where(x => !string.IsNullOrWhiteSpace(x.User.NationalIdentityNumber))
            .GroupBy(x => x.User.UserId)
            .ToDictionary(x => x.Key, x => x.Select(xx => xx.User.NationalIdentityNumber).First());
        
        createdActivity = events
            .InstanceEvents
            .OrderBy(x => x.Created)
            .Where(x => x.DataId is null) // The created event is not associated with a data element
            .Where(x => StringComparer.OrdinalIgnoreCase.Equals(x.EventType, "created"))
            .Select(x => new ActivityDto
            {
                Id = x.Id.Value.ToVersion7(x.Created.Value),
                Type = DialogActivityType.DialogCreated,
                CreatedAt = x.Created,
                PerformedBy = string.IsNullOrWhiteSpace(x.User.OrgId) 
                    ? new() { ActorType = ActorType.ServiceOwner }
                    : new()
                    {
                        ActorType = ActorType.PartyRepresentative, 
                        ActorId = nationalIdentityNumberByUserId.TryGetValue(x.User.UserId, out var nationalId) 
                            ? ToPersonIdentifier(nationalId)
                            // TODO: Fallback userId to national identity number api in storage
                            : throw new InvalidOperationException()  
                    }
            })
            .FirstOrDefault();

        return createdActivity is not null;
    }

    private static string ToParty(InstanceOwner instanceOwner)
    {
        return ToPersonIdentifier(instanceOwner.PersonNumber)
            ?? ToOrgIdentifier(instanceOwner.OrganisationNumber)
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

    private static string ToServiceResource(ReadOnlySpan<char> appId)
    {
        const string urnPrefix = "urn:altinn:resource:app_";
        Span<char> span = stackalloc char[appId.Length + urnPrefix.Length];
        appId.CopyTo(span[^appId.Length..]);
        span.Replace('/', '_');
        urnPrefix.CopyTo(span);
        return span.ToString();
    }

    public static string ToTitle(ReadOnlySpan<char> title, IReadOnlyCollection<string>? presentationTexts)
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
}
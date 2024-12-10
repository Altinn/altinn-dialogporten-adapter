using System.Diagnostics.CodeAnalysis;
using Altinn.Platform.DialogportenAdapter.WebApi.Common;
using Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
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
        var platformBaseUri = _settings.Infrastructure.Altinn.PlatformBaseUri;
        var appBaseUri = _settings.Infrastructure.Altinn.GetAppUriForOrg(instance.Org);
        // Opprettet
        // Rising edge saved activity
        // Sent to signing (information)
        // Signed (information)
        // 
        // Get nationalIdentityNumber from userId api request from storage 
        
        
        var dialog = new DialogDto
        {
            Id = dialogId,
            Party = ToParty(instance.InstanceOwner),
            ServiceResource = ToServiceResource(instance.AppId),
            CreatedAt = instance.Created,
            VisibleFrom = instance.VisibleAfter > DateTimeOffset.UtcNow ? instance.VisibleAfter : null,
            DueAt = instance.DueBefore < DateTimeOffset.UtcNow ? instance.DueBefore : null,
            ExternalReference = $"{instance.Id}",
            Status = instance.Process.CurrentTask.AltinnTaskType switch
            {
                _ when instance.Status.IsArchived => DialogStatus.Completed,
                "Reject" => DialogStatus.RequiresAttention,
                _ => DialogStatus.Draft
            },
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
            GuiActions = [
                instance.Status.IsArchived 
                    ? new()
                    {
                        Action = "read",
                        Priority = DialogGuiActionPriority.Primary,
                        Title = [new(){LanguageCode = "nb", Value = "Se innsendt skjema"}],
                        Url = new($"{platformBaseUri}/receipt/{instance.Id}")
                    }
                    : new()
                    {
                        Action = "write",
                        Priority = DialogGuiActionPriority.Primary,
                        Title = [new(){LanguageCode = "nb", Value = "Gå til skjemautfylling"}],
                        Url = new($"{appBaseUri}/{instance.AppId}/#/instance/{instance.Id}")
                    },
                // TODO: Eksponer slette api i adapter som tar i mot dialog token og sletter dialogen
                new()
                {
                    // TODO: Verifiser XACML action for å slette dialogen
                    Action = "write",
                    Priority = DialogGuiActionPriority.Secondary,
                    IsDeleteDialogAction = true,
                    Title = [new(){LanguageCode = "nb", Value = "Slett skjema"}],
                    // TODO: Ikke sett prompt dersom det er en draft (ikke arkivert?)
                    Prompt = [new(){LanguageCode = "nb", Value = "Skjemaet blir permanent slettet"}], 
                    // TODO: Endre til delete-url (bruk /sbl/instances/:instanceOwnerPartyId/:instanceGuid?hard=<boolean>)
                    Url = new($"{platformBaseUri}/{instance.AppId}/#/instance/{instance.Id}") 
                }
            ],
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
                        Url = new(x.SelfLinks.Platform)
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

    private static bool TryGetCreatedActivity(InstanceEventList events, [NotNullWhen(true)] out ActivityDto? createdActivity)
    {
        var nationalIdentityNumberByUserId = events.InstanceEvents
            .Where(x => !string.IsNullOrWhiteSpace(x.User.NationalIdentityNumber))
            .GroupBy(x => x.User.UserId)
            .ToDictionary(x => x.Key, x => x.Select(xx => xx.User.NationalIdentityNumber).Single());
        
        createdActivity = events
            .InstanceEvents
            .OrderBy(x => x.Created)
            // .Where(x => // Service owner initiated dialog
            //     (StringComparer.OrdinalIgnoreCase.Equals(x.EventType, "created") && !string.IsNullOrWhiteSpace(x.User.OrgId))
            //     // End user initiated dialog
            //     // For some reason the created event does not have the user's national identity number,
            //     // therefore we need to use the first process_StartEvent when the instance is user initiated
            //     || (StringComparer.OrdinalIgnoreCase.Equals(x.EventType, "process_StartEvent") && !string.IsNullOrWhiteSpace(x.User.NationalIdentityNumber)))
            .Where(x => StringComparer.OrdinalIgnoreCase.Equals(x.EventType, "created"))
            .Select(x => new ActivityDto
            {
                Id = x.Id.Value.ToVersion7(x.Created.Value),
                Type = DialogActivityType.DialogCreated,
                CreatedAt = x.Created,
                PerformedBy = new()
                    {
                        ActorId = string.IsNullOrWhiteSpace(x.User.OrgId) 
                                  && nationalIdentityNumberByUserId.TryGetValue(x.User.UserId, out var nationalId) 
                                    ? ToPersonIdentifier(nationalId)
                                    : throw new InvalidOperationException(), // TODO: Fallback userId to national identity number api in storage
                        ActorType = !string.IsNullOrWhiteSpace(x.User.OrgId)
                            ? ActorType.ServiceOwner
                            : ActorType.PartyRepresentative
                    }
            })
            .FirstOrDefault(x => x.PerformedBy.ActorId is not null);

        return createdActivity is not null;
    }

    // private string ToPersonIdentifier(User user, Dictionary<string, string> cachedValuesFromEvent)
    // {
    //     
    // }

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
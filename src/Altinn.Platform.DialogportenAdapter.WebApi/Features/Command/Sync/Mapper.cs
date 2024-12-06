using Altinn.Platform.DialogportenAdapter.WebApi.Common;
using Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.Platform.DialogportenAdapter.WebApi.Features.Command.Sync;

internal static class Mapper
{
    public static DialogDto CreateOrUpdate(
        this DialogDto? existing, 
        Guid dialogId, 
        Application application,
        Instance instance,
        InstanceEventList events)
    {
        var storageDialog = instance.ToDialogDto(dialogId, application);
        if (existing is null)
        {
            return storageDialog;
        }
        
        // TODO: Merge the existing dialog with the storage dialog
        storageDialog.Revision = existing.Revision;
        return storageDialog;
    }
    
    private  static DialogDto ToDialogDto(this Instance instance, Guid dialogId, Application application)
    {
        // TODO: Flytt til konfigurasjon. Denne burde kanskje ikke lenger være statisk - begynner å bli mye mer enn en mapper.
        const string appBaseUri = "https://digdir.apps.tt02.altinn.no";
        
        return new DialogDto
        {
            Id = dialogId,
            Party = instance.InstanceOwner.ToParty(),
            ServiceResource = ToServiceResource(instance.AppId),
            CreatedAt = instance.Created,
            VisibleFrom = instance.VisibleAfter > DateTimeOffset.UtcNow ? instance.VisibleAfter : null,
            DueAt = instance.DueBefore < DateTimeOffset.UtcNow ? instance.DueBefore : null,
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
                new()
                {
                    Action = "Open",
                    Priority = DialogGuiActionPriority.Primary,
                    Title = [new(){LanguageCode = "nb", Value = "Gå til skjemautfylling"}],
                    Url = new($"{appBaseUri}/{instance.AppId}/#/instance/{instance.Id}")
                },
                // TODO: Skal vi ha slett her?
                new()
                {
                    Action = "Delete",
                    Priority = DialogGuiActionPriority.Secondary,
                    IsDeleteDialogAction = true,
                    Title = [new(){LanguageCode = "nb", Value = "Slett skjema"}],
                    Prompt = [new(){LanguageCode = "nb", Value = "Skjemaet blir permanent slettet"}],
                    // TODO: Endre til delete-url
                    Url = new($"{appBaseUri}/{instance.AppId}/#/instance/{instance.Id}") 
                }
            ],
            Attachments = instance.Data
                .Where(x => x.Filename is not null)
                .Select(x => new AttachmentDto
                {
                    DisplayName = [new() {LanguageCode = "nb", Value = x.Filename!}],
                    Urls = [new()
                    {
                        ConsumerType = AttachmentUrlConsumerType.Gui, 
                        MediaType = x.ContentType, 
                        // TODO: Endre til riktig url
                        Url = new($"{appBaseUri}/storage/api/v1/instances/{instance.Id}/data/{x.Id}")
                    }],
                })
                .ToList()
        };
    }
    
    private static string ToParty(this InstanceOwner instanceOwner)
    {
        const string personPrefix = "urn:altinn:person:identifier-no:";
        const string orgPrefix = "urn:altinn:organization:identifier-no:";

        if (instanceOwner.PersonNumber is not null)
        {
            return $"{personPrefix}{instanceOwner.PersonNumber}";
        }
    
        if (instanceOwner.OrganisationNumber is not null)
        {
            return $"{orgPrefix}{instanceOwner.OrganisationNumber}";
        }
    
        throw new ArgumentException("Instance owner must have either a person number or an organisation number");
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
    
    /// <summary>
    /// Will try to copy the source span to the destination span from offset, and return true if the entire source span was copied.
    /// If the destination span reminder is too small, the source span will be truncated and "..." will be appended to the destination span.
    /// </summary>
    /// <param name="source">The span to copy from.</param>
    /// <param name="destination">The span to copy to.</param>
    /// <param name="offset">The offset in the destination span to start copying to. Will be updated with the new offset after copying.</param>
    /// <returns>True if the entire source span was copied, false otherwise.</returns>
    private static bool TryCopyTo(this ReadOnlySpan<char> source, Span<char> destination, ref int offset)
    {
        const string andMore = "...";
        var remaining = destination.Length - offset;
        if (remaining <= source.Length)
        {
            source[..Math.Max(remaining - andMore.Length, 0)].CopyTo(destination[offset..]);
            andMore.CopyTo(destination[^andMore.Length..]);
            offset = destination.Length;
            return false;
        }
        
        source.CopyTo(destination[offset..]);
        offset += source.Length;
        return true;
    }
}

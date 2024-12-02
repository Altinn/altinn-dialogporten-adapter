using Altinn.Platform.DialogportenAdapter.WebApi.Common;
using Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.Platform.DialogportenAdapter.WebApi.Features.Command.Sync;

internal static class Mapper
{
    public static DialogDto CreateOrUpdate(
        this DialogDto? existing, 
        Guid dialogId, 
        Instance instance,
        Application application)
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
        return new DialogDto
        {
            Id = dialogId,
            Party = instance.InstanceOwner.ToParty(),
            ServiceResource = ToServiceResource(instance.AppId),
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
                    Value = [new() { LanguageCode = "nb", Value = "Påkrevd, men vi har ikke noe fra Storage..." }]
                }
            }
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

    private static string ToTitle(ReadOnlySpan<char> title, IReadOnlyCollection<string>? presentationTexts)
    {
        const string separator = ", ";
        if (presentationTexts is null)
        {
            return title.ToString();
        }
        
        var presentationTextLength = presentationTexts.Sum(x => x.Length) + presentationTexts.Count * separator.Length;
        Span<char> titleSpan = stackalloc char[title.Length + presentationTextLength];
        title.CopyTo(titleSpan);
    
        var offset = title.Length;
        foreach (var text in presentationTexts)
        {
            separator.CopyTo(titleSpan[offset..]);
            offset += separator.Length;
            text.CopyTo(titleSpan[offset..]);
            offset += text.Length;
        }
    
        return titleSpan.ToString();
    }
}

using System.Net;
using Altinn.Platform.DialogportenAdapter.WebApi.Common;
using Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.Platform.DialogportenAdapter.WebApi.Features.Command.Sync;

internal class SyncInstanceToDialogService
{
    private readonly IStorageApi _storageApi;
    private readonly IDialogportenApi _dialogportenApi;
    
    public SyncInstanceToDialogService(IStorageApi storageApi, IDialogportenApi dialogportenApi)
    {
        _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
        _dialogportenApi = dialogportenApi ?? throw new ArgumentNullException(nameof(dialogportenApi));
    }
    
    public async Task Sync(SyncInstanceToDialogDto dto, CancellationToken cancellationToken = default)
    {
        var instance = await _storageApi.GetInstance(dto.PartyId, dto.InstanceId, cancellationToken);
        var application = await _storageApi.GetApplication(instance.AppId, cancellationToken);
            
        var dialogId = dto.InstanceId.ToVersion7(instance.Created.Value);
        var getDialogResult = await _dialogportenApi.Get(dialogId, cancellationToken);
        if (getDialogResult.StatusCode == HttpStatusCode.NotFound)
        {
            await CreateDialog(cancellationToken, dialogId, instance, application);
        }

        var dialog = getDialogResult.Content;
        
        return Results.Ok(dialog);
    }

    private async Task CreateDialog(Guid dialogId, 
        Instance instance,
        Application application,
        CancellationToken cancellationToken)
    {
        var newDialog = new DialogDto
        {
            Id = dialogId,
            Party = ToParty(instance.InstanceOwner),
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

        await _dialogportenApi.Create(newDialog, cancellationToken);
    }

    private static string ToParty(InstanceOwner instanceOwner)
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
        //  👇 blir denne til .................... 👇 denne?
        // digdir/soknad-om-stimulabmidler => app_digdir_soknad-om-stimulabmidler => urn:altinn:resource:app_digdir_soknad-om-stimulabmidler
        const string urnPrefix = "urn:altinn:resource:app_";
        Span<char> span = stackalloc char[appId.Length + urnPrefix.Length];
        appId.CopyTo(span[^appId.Length..]);
        span.Replace('/', '_');
        urnPrefix.CopyTo(span);
        return span.ToString();
    }

    private static string ToTitle(ReadOnlySpan<char> title, IReadOnlyCollection<string>? presentationTexts)
    {
        presentationTexts ??= [];
        const string separator = ", ";
        var presentationTextLength = presentationTexts.Sum(x => x.Length) + presentationTexts.Count * separator.Length;
        Span<char> titleSpan = stackalloc char[title.Length + presentationTextLength];
        title.CopyTo(titleSpan);
    
        if (presentationTexts.Count == 0)
        {
            return titleSpan.ToString();
        }
    
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
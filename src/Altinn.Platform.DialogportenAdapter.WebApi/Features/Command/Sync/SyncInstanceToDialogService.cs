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
    
    public async Task<Guid> Sync(SyncInstanceToDialogDto dto, CancellationToken cancellationToken = default)
    {
        // Get the instance from storage
        var instance = await _storageApi.GetInstance(dto.PartyId, dto.InstanceId, cancellationToken);
        
        // Create a uuid7 from the instance id and created timestamp to use as dialog id
        var dialogId = dto.InstanceId.ToVersion7(instance.Created.Value);

        // Fetch events, application and existing dialog in parallel
        var (existingDialog, application, events) = await (
            GetDialog(dialogId, cancellationToken),
            _storageApi.GetApplication(instance.AppId, cancellationToken),
            _storageApi.GetInstanceEvents(dto.PartyId, dto.InstanceId, cancellationToken)
        );

        // Create or update the dialog with the fetched data
        var updatedDialog = existingDialog.CreateOrUpdate(dialogId, application, instance, events);
        
        // Upsert the dialog and update the instance with the dialog id
        await UpsertDialog(updatedDialog, cancellationToken);
        //await UpdateInstanceWithDialogId(dto, dialogId, cancellationToken);
        return dialogId;
    }

    private async Task<DialogDto?> GetDialog(Guid dialogId, CancellationToken cancellationToken)
    {
        var getDialogResult = await _dialogportenApi.Get(dialogId, cancellationToken);
        
        if (getDialogResult.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        
        return getDialogResult.IsSuccessful
            ? getDialogResult.Content
            : throw getDialogResult.Error;
    }

    private Task UpsertDialog(DialogDto dialog, CancellationToken cancellationToken) =>
        !dialog.Revision.HasValue
            ? _dialogportenApi.Create(dialog, cancellationToken)
            : _dialogportenApi.Update(dialog, dialog.Revision!.Value, cancellationToken);

    private Task UpdateInstanceWithDialogId(SyncInstanceToDialogDto dto, Guid dialogId,
        CancellationToken cancellationToken)
    {
        var dataValues = new DataValues
        {
            Values = new() { { "dialogId", dialogId.ToString() } }
        };
        return _storageApi.UpdateDataValues(dto.PartyId, dto.InstanceId, dataValues, cancellationToken);
    }
}
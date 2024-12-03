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
    
    public async Task<DialogDto> Sync(SyncInstanceToDialogDto dto, CancellationToken cancellationToken = default)
    {
        var instance = await _storageApi.GetInstance(dto.PartyId, dto.InstanceId, cancellationToken);
        var application = await _storageApi.GetApplication(instance.AppId, cancellationToken);
        var dialogId = dto.InstanceId.ToVersion7(instance.Created.Value);
        
        var existingDialog = await GetDialog(dialogId, cancellationToken);
        var updatedDialog = existingDialog.CreateOrUpdate(dialogId, instance, application);
        
        await UpsertDialog(updatedDialog, cancellationToken);
        await UpdateInstanceWithDialogId(dto, dialogId, cancellationToken);
        return updatedDialog;
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

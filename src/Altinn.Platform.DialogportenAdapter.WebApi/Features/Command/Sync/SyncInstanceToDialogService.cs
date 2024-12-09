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
    private readonly StorageDialogportenDataMerger _dataMerger;
    
    public SyncInstanceToDialogService(
        IStorageApi storageApi,
        IDialogportenApi dialogportenApi,
        StorageDialogportenDataMerger dataMerger)
    {
        _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
        _dialogportenApi = dialogportenApi ?? throw new ArgumentNullException(nameof(dialogportenApi));
        _dataMerger = dataMerger ?? throw new ArgumentNullException(nameof(dataMerger));
    }
    
    public async Task<Guid> Sync(SyncInstanceToDialogDto dto, CancellationToken cancellationToken = default)
    {
        // Create a uuid7 from the instance id and created timestamp to use as dialog id
        var dialogId = dto.InstanceId.ToVersion7(dto.InstanceCreatedAt);

        // Fetch events, application, instance and existing dialog in parallel
        var (existingDialog, application, instance, events) = await (
            GetDialog(dialogId, cancellationToken),
            _storageApi.GetApplication(dto.AppId, cancellationToken),
            _storageApi.GetInstance(dto.PartyId, dto.InstanceId, cancellationToken),
            _storageApi.GetInstanceEvents(dto.PartyId, dto.InstanceId, cancellationToken)
        );
        
        // TODO: Hva om vi ikke finner instance?
        // !instance && !dialog => noop (warning)
        // !instance && dialog => purge dialog
        // instance && !dialog => create dialog
        // instance && dialog => update dialog (restore/merge/delete)

        // Create or update the dialog with the fetched data
        var updatedDialog = _dataMerger.Merge(dialogId, existingDialog, application, instance, events);
        
        // Upsert the dialog and update the instance with the dialog id
        await UpsertDialog(updatedDialog, cancellationToken);
        await UpdateInstanceWithDialogId(dto, dialogId, cancellationToken);
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
            Values = new() { { "dialogId", dialogId.ToString() } } // TODO: Last synced at? 
        };
        return _storageApi.UpdateDataValues(dto.PartyId, dto.InstanceId, dataValues, cancellationToken);
    }
}
        
// 1: Slett via gui action i AF
// 2: Slett i Dialogporten adapter som verifiserer dialog token
// 3: Kall /sbl/instances/:instanceOwnerPartyId/:instanceGuid?hard=<boolean>
// 4: Storage sender slett event til dialogporten adapter (instanceId og CreatedAt)
// 5: Dialogporten adapter sletter dialogen via dialogport api
// 6: Dialogporten vil via GQL subscription si i fra til nettleser at dialogen er slettet
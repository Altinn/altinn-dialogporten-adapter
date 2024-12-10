using System.Diagnostics;
using Altinn.Platform.DialogportenAdapter.WebApi.Common;
using Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Storage;

namespace Altinn.Platform.DialogportenAdapter.WebApi.Features.Command.Sync;

public record SyncInstanceToDialogDto(
    string AppId,
    int PartyId, 
    Guid InstanceId,
    DateTimeOffset InstanceCreatedAt);

internal class SyncInstanceToDialogService
{
    private readonly IStorageApi _storageApi;
    private readonly IDialogportenApi _dialogportenApi;
    private readonly StorageDialogportenDataMerger _dataMerger;
    private readonly ILogger<SyncInstanceToDialogService> _logger;
    
    public SyncInstanceToDialogService(
        IStorageApi storageApi,
        IDialogportenApi dialogportenApi,
        StorageDialogportenDataMerger dataMerger, 
        ILogger<SyncInstanceToDialogService> logger)
    {
        _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
        _dialogportenApi = dialogportenApi ?? throw new ArgumentNullException(nameof(dialogportenApi));
        _dataMerger = dataMerger ?? throw new ArgumentNullException(nameof(dataMerger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task Sync(SyncInstanceToDialogDto dto, CancellationToken cancellationToken = default)
    {
        // Create a uuid7 from the instance id and created timestamp to use as dialog id
        var dialogId = dto.InstanceId.ToVersion7(dto.InstanceCreatedAt);

        // Fetch events, application, instance and existing dialog in parallel
        var (application, existingDialog, instance, events) = await (
            _storageApi.GetApplication(dto.AppId, cancellationToken),
            _dialogportenApi.Get(dialogId, cancellationToken).ContentOrDefault(),
            _storageApi.GetInstance(dto.PartyId, dto.InstanceId, cancellationToken).ContentOrDefault(),
            _storageApi.GetInstanceEvents(dto.PartyId, dto.InstanceId, Constants.SupportedEventTypes, cancellationToken).ContentOrDefault()
        );

        switch (instance)
        {
            case null or { Status.IsHardDeleted: true } when existingDialog is not null:
                await _dialogportenApi.Purge(existingDialog.Id!.Value, existingDialog.Revision!.Value, cancellationToken);
                return;
            case { Status.IsSoftDeleted: true } when existingDialog is { Deleted: true }:
                // Dialogporten does not allow updating a soft deleted
                // dialog so there is nothing to do and we can return
                return;
            case { Status.IsSoftDeleted: false } when existingDialog is { Deleted: true }:
                // TODO: Restore dialog
                // TODO: Her hadde det vært gunstig å få ny revisjon fra dialogporten response headers slik at vi ikke trenger å hente ny dialog for hver gang vi gjær noe mot apiet.
                break;
            case null:
                _logger.LogWarning("No dialog or instance found for request {@Request}.", dto);
                return;
            case not null when events is null:
                throw new UnreachableException("Events should always exist when instance exists.");
        }

        // instance && !dialog => create dialog
        // instance && dialog => update dialog (restore/merge/delete)
        // instance.Status.IsSoftDeleted && !dialog.Deleted => soft delete dialog
        // instance.Status.IsSoftDeleted && !dialog.Deleted => soft delete dialog
        // 1. Soft delete dialog
        // 2. Restore dialog
        // 3. Merge dialog
        // 4. Purge dialog

        // Create or update the dialog with the fetched data
        var updatedDialog = _dataMerger.Merge(dialogId, existingDialog, application, instance, events);

        await Task.WhenAll(
            UpsertDialog(updatedDialog, cancellationToken),
            UpdateInstanceWithDialogId(dto, dialogId, cancellationToken)
        ).WithAggregatedExceptions();
        
        if (instance.Status.IsSoftDeleted)
        {
            await _dialogportenApi.Delete(existingDialog!.Id!.Value, existingDialog.Revision!.Value, cancellationToken);
        }
    }

    private Task UpsertDialog(DialogDto dialog, CancellationToken cancellationToken)
    {
        return !dialog.Revision.HasValue
            ? _dialogportenApi.Create(dialog, cancellationToken)
            : _dialogportenApi.Update(dialog, dialog.Revision!.Value, cancellationToken);
    }

    private Task UpdateInstanceWithDialogId(SyncInstanceToDialogDto dto, Guid dialogId,
        CancellationToken cancellationToken)
    {
        return _storageApi.UpdateDataValues(dto.PartyId, dto.InstanceId, new()
        {
            Values = new()
            {
                { "dialog.id", dialogId.ToString() },
                { "dialog.syncedAt", DateTimeOffset.UtcNow.ToString("O") }
            }
        }, cancellationToken);
    }
}
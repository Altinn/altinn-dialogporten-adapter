using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Altinn.Platform.DialogportenAdapter.WebApi.Common;
using Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;

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
        var (existingDialog, application, instance, events) = await (
            _dialogportenApi.Get(dialogId, cancellationToken).ContentOrDefault(),
            _storageApi.GetApplication(dto.AppId, cancellationToken).ContentOrDefault(),
            _storageApi.GetInstance(dto.PartyId, dto.InstanceId, cancellationToken).ContentOrDefault(),
            _storageApi.GetInstanceEvents(dto.PartyId, dto.InstanceId, Constants.SupportedEventTypes, cancellationToken).ContentOrDefault()
        );
        
        if (instance is null && existingDialog is null)
        {
            _logger.LogWarning("No dialog or instance found for request {@Request}.", dto);
            return;
        }

        if (BothIsSoftDeleted(instance, existingDialog))
        {
            return;
        }

        if (ShouldPurgeDialog(instance, existingDialog))
        {
            await _dialogportenApi.Purge(dialogId, existingDialog.Revision!.Value, cancellationToken);
            return;
        }

        if (ShouldRestoreDialog(instance, existingDialog))
        {
            // TODO: Restore dialog
            // TODO: Her hadde det vært gunstig å få ny revisjon fra dialogporten response headers slik at vi ikke trenger å hente ny dialog for hver gang vi gjær noe mot apiet.
            throw new NotSupportedException();
        }

        EnsureNotNull(application, instance, events);

        // Create or update the dialog with the fetched data
        var updatedDialog = _dataMerger.Merge(dialogId, existingDialog, application, instance, events);
        await UpsertDialog(updatedDialog, cancellationToken);
        await UpdateInstanceWithDialogId(dto, dialogId, cancellationToken);
        if (instance.Status.IsSoftDeleted)
        {
            await _dialogportenApi.Delete(dialogId, updatedDialog.Revision!.Value, cancellationToken);
        }
    }

    private static void EnsureNotNull(
        [NotNull] Application? application, 
        [NotNull] Instance? instance, 
        [NotNull] InstanceEventList? events)
    {
        if (application is null || instance is null || events is null)
        {
            throw new UnreachableException(
                $"Application ({application is not null}), " +
                $"instance ({instance is not null}) " +
                $"and events ({events is not null}) " +
                $"should exist at this point.");
        }
    }

    private static bool BothIsSoftDeleted([NotNullWhen(true)] Instance? instance, [NotNullWhen(true)] DialogDto? existingDialog)
    {
        return instance is { Status.IsSoftDeleted: true } && existingDialog is { Deleted: true };
    }

    private static bool ShouldRestoreDialog([NotNullWhen(true)] Instance? instance, [NotNullWhen(true)] DialogDto? existingDialog)
    {
        return instance is { Status.IsSoftDeleted: false } && existingDialog is { Deleted: true };
    }

    private static bool ShouldPurgeDialog(Instance? instance, [NotNullWhen(true)] DialogDto? existingDialog)
    {
        return instance is null or { Status.IsSoftDeleted: true } && existingDialog is not null;
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
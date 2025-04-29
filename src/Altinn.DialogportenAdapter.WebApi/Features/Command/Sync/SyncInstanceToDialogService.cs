using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

public record SyncInstanceToDialogDto(
    string AppId,
    int PartyId,
    Guid InstanceId,
    DateTimeOffset InstanceCreatedAt,
    bool IsMigration);

internal sealed class SyncInstanceToDialogService
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
            _logger.LogWarning("No dialog or instance found for request. {PartyId},{InstanceId},{InstanceCreatedAt},{IsMigration}.",
                dto.PartyId,
                dto.InstanceId,
                dto.InstanceCreatedAt,
                dto.IsMigration);
            return;
        }

        if (BothIsDeleted(instance, existingDialog))
        {
            return;
        }

        if (ShouldUpdateInstanceWithDialogId(instance, dialogId))
        {
            // Update the instance with the dialogId before we start to modify the dialog
            // This way we can keep track of which instances that have been attempted synced
            // to dialogporten even if the dialogporten api is down or we have a bug in the
            // sync process.
            await UpdateInstanceWithDialogId(dto, dialogId, cancellationToken);
        }

        if (IsDialogSyncDisabled(instance))
        {
            return;
        }

        if (!SyncAdapterSettings.Instance.DisableDelete && ShouldPurgeDialog(instance, existingDialog))
        {
            await _dialogportenApi.Purge(
                dialogId,
                existingDialog.Revision!.Value,
                isSilentUpdate: dto.IsMigration,
                cancellationToken: cancellationToken);
            return;
        }

        if (!SyncAdapterSettings.Instance.DisableDelete && ShouldSoftDeleteDialog(instance, existingDialog))
        {
            await _dialogportenApi.Delete(
                dialogId,
                existingDialog.Revision!.Value,
                isSilentUpdate: dto.IsMigration,
                cancellationToken: cancellationToken);
            return;
        }

        if (ShouldRestoreDialog(instance, existingDialog))
        {
            existingDialog.Revision = await RestoreDialog(dialogId,
                existingDialog.Revision!.Value,
                disableAltinnEvents: dto.IsMigration,
                cancellationToken);
            existingDialog.Deleted = false;
        }

        EnsureNotNull(application, instance, events);

        if (SyncAdapterSettings.Instance.DisableSync)
        {
            return;
        }

        // Create or update the dialog with the fetched data
        var updatedDialog = await _dataMerger.Merge(dialogId, existingDialog, application, instance, events, dto.IsMigration);
        await UpsertDialog(updatedDialog, isMigration: dto.IsMigration, cancellationToken);
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

    private static bool ShouldSoftDeleteDialog([NotNullWhen(true)] Instance? instance, [NotNullWhen(true)] DialogDto? existingDialog)
    {
        return instance is { Status.IsSoftDeleted: true } && existingDialog is not null;
    }

    private static bool IsDialogSyncDisabled(Instance? instance)
    {
        return instance?.DataValues is not null
               && instance.DataValues.TryGetValue(Constants.InstanceDataValueDisableSyncKey, out var disableSyncString)
               && bool.TryParse(disableSyncString, out var disableSync)
               && disableSync;
    }

    private static bool BothIsDeleted(Instance? instance, DialogDto? existingDialog)
    {
        return BothIsHardDeleted(instance, existingDialog) || BothIsSoftDeleted(instance, existingDialog);
    }

    private static bool BothIsHardDeleted(Instance? instance, [NotNullWhen(false)] DialogDto? existingDialog)
    {
        return instance is null or { Status.IsHardDeleted: true } && existingDialog is null;
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
        return instance is null or { Status.IsHardDeleted: true } && existingDialog is not null;
    }

    private static bool ShouldUpdateInstanceWithDialogId([NotNullWhen(true)] Instance? instance, Guid dialogId)
    {
        return instance?.DataValues is null
           || !instance.DataValues.TryGetValue(Constants.InstanceDataValueDialogIdKey, out var dialogIdString)
           || !Guid.TryParse(dialogIdString, out var instanceDialogId)
           || instanceDialogId != dialogId;
    }

    private Task UpsertDialog(DialogDto dialog, bool isMigration, CancellationToken cancellationToken)
    {
        return !dialog.Revision.HasValue
            ? _dialogportenApi.Create(dialog, isSilentUpdate: isMigration, cancellationToken: cancellationToken)
            : _dialogportenApi.Update(dialog, dialog.Revision!.Value, isSilentUpdate: isMigration, cancellationToken: cancellationToken);
    }

    private async Task<Guid> RestoreDialog(Guid dialogId,
        Guid revision,
        bool disableAltinnEvents,
        CancellationToken cancellationToken)
    {
        var response = await _dialogportenApi
            .Restore(dialogId, revision, disableAltinnEvents, cancellationToken)
            .EnsureSuccess();

        if (!response.Headers.TryGetValues(IDialogportenApi.ETagHeader, out var etags))
        {
            throw new UnreachableException("ETag header was not found.");
        }

        if (!Guid.TryParse(etags.FirstOrDefault(), out var etag))
        {
            throw new UnreachableException("ETag header could not be parsed.");
        }

        return etag;
    }

    private Task UpdateInstanceWithDialogId(SyncInstanceToDialogDto dto, Guid dialogId,
        CancellationToken cancellationToken)
    {
        return _storageApi.UpdateDataValues(dto.PartyId, dto.InstanceId, new()
        {
            Values = new()
            {
                { Constants.InstanceDataValueDialogIdKey, dialogId.ToString() }
            }
        }, cancellationToken);
    }
}
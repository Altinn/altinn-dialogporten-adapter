using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;
using Constants = Altinn.DialogportenAdapter.WebApi.Common.Constants;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

public interface ISyncInstanceToDialogService
{
    Task Sync(SyncInstanceCommand dto, CancellationToken cancellationToken = default);
}

internal sealed class SyncInstanceToDialogService : ISyncInstanceToDialogService
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

    public async Task Sync(SyncInstanceCommand dto, CancellationToken cancellationToken = default)
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

        if (ShouldUpdateInstanceWithDialogId(instance, dialogId))
        {
            // Update the instance with the dialogId before we start to modify the dialog
            // This way we can keep track of which instances that have been attempted synced
            // to dialogporten even if the dialogporten api is down or we have a bug in the
            // sync process.
            await UpdateInstanceWithDialogId(dto, dialogId, cancellationToken);
        }

        if (InstanceOwnerIsSelfIdentified(instance))
        {
            // We skip these for now as we do not have a good way to identify the user in dialogporten
            _logger.LogWarning("Skipping sync for self-identified instance owner on id={Id} username={Username} appid={AppId}.",
                instance?.Id,
                instance?.InstanceOwner.Username,
                instance?.AppId);
            return;
        }

        if (BothIsDeleted(instance, existingDialog))
        {
            return;
        }

        var forceSilentUpsert = false;
        var shouldDeleteAfterCreate = false;
        if (InstanceSoftDeletedAndDialogNotExisting(instance, existingDialog))
        {
            _logger.LogInformation(
                "Instance id={Id} is soft-deleted in storage and does not exist in Dialogporten. Creating and deleting immediately afterwards.",
                instance?.Id);
            forceSilentUpsert = true;
            shouldDeleteAfterCreate = true;
        }

        var syncAdapterSettings = application.GetSyncAdapterSettings();
        if (syncAdapterSettings.DisableSync || IsDialogSyncDisabled(instance))
        {
            return;
        }

        if (ShouldPurgeDialog(instance, existingDialog))
        {
            if (syncAdapterSettings.DisableDelete) return;
            await _dialogportenApi.Purge(
                dialogId,
                existingDialog.Revision!.Value,
                isSilentUpdate: dto.IsMigration,
                cancellationToken: cancellationToken);
            return;
        }

        if (ShouldSoftDeleteDialog(instance, existingDialog))
        {
            if (syncAdapterSettings.DisableDelete) return;
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

        // Create or update the dialog with the fetched data
        var mergeDto = new MergeDto(dialogId, existingDialog, application, instance, events, dto.IsMigration || forceSilentUpsert);
        var updatedDialog = await _dataMerger.Merge(mergeDto, cancellationToken);
        var revision = await UpsertDialog(updatedDialog, existingDialog, syncAdapterSettings, dto.IsMigration || forceSilentUpsert, cancellationToken);

        if (!syncAdapterSettings.DisableDelete && shouldDeleteAfterCreate && revision.HasValue)
        {
            await _dialogportenApi.Delete(
                dialogId,
                revision.Value,
                isSilentUpdate: true,
                cancellationToken: cancellationToken);
        }
    }

    private static bool InstanceOwnerIsSelfIdentified(Instance? instance)
    {
        return instance is not null
               && instance.InstanceOwner.OrganisationNumber is null
               && instance.InstanceOwner.PersonNumber is null
               && instance.InstanceOwner.PartyId is not null
               && instance.InstanceOwner.Username is not null;
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

    private static bool InstanceSoftDeletedAndDialogNotExisting(Instance? instance, DialogDto? existingDialog)
    {
        return instance is { Status.IsSoftDeleted: true } && existingDialog is null;
    }

    private static bool ShouldUpdateInstanceWithDialogId([NotNullWhen(true)] Instance? instance, Guid dialogId)
    {
        if (instance is null)
        {
            return false;
        }

        return instance.DataValues is null
           || !instance.DataValues.TryGetValue(Constants.InstanceDataValueDialogIdKey, out var dialogIdString)
           || !Guid.TryParse(dialogIdString, out var instanceDialogId)
           || instanceDialogId != dialogId;
    }

    private async Task<Guid?> UpsertDialog(DialogDto updated,
        DialogDto? existing,
        SyncAdapterSettings settings,
        bool isMigration,
        CancellationToken cancellationToken) =>
        existing is null
            ? await CreateDialog(updated, settings, isMigration, cancellationToken)
            : await UpdateDialog(updated, existing, isMigration, cancellationToken);

    private async Task<Guid?> CreateDialog(
        DialogDto dto,
        SyncAdapterSettings settings,
        bool isMigration,
        CancellationToken cancellationToken)
    {
        if (settings.DisableCreate) return null;

        var createResult = await _dialogportenApi
            .Create(dto, isSilentUpdate: isMigration, cancellationToken: cancellationToken)
            .EnsureSuccess();

        return createResult.GetEtagHeader();
    }

    // PostgreSQL has a minimum time precision of 1 microsecond. To avoid issues with updates where the CreatedAt time is changed by less than this precision,
    // we define an epsilon value of 1 microsecond to use when comparing timestamps.
    private static readonly TimeSpan Epsilon = TimeSpan.FromMicroseconds(1);
    private async Task<Guid> UpdateDialog(DialogDto updated, DialogDto? existing, bool isMigration,
        CancellationToken cancellationToken)
    {
        var activityUpdateRequests = existing?.Activities
            .Join(updated.Activities, x => x.Id, x => x.Id, (prev, next) => (prev, next))
            .Where(x => x.prev.Type == DialogActivityType.FormSaved && (x.next.CreatedAt!.Value - x.prev.CreatedAt!.Value) > Epsilon)
            .Select(x => new { ActivityId = x.next.Id!.Value, NewCreatedAt = x.next.CreatedAt!.Value })
            .ToArray() ?? [];

        PruneExistingImmutableEntities(updated, existing);

        var updateResult = await _dialogportenApi.Update(updated, updated.Revision!.Value,
            isSilentUpdate: isMigration,
            cancellationToken: cancellationToken).EnsureSuccess();

        updated.Revision = updateResult.GetEtagHeader();

        foreach (var activityUpdateRequest in activityUpdateRequests)
        {
            var result = await _dialogportenApi.UpdateFormSavedActivityTime(
                updated.Id!.Value,
                activityUpdateRequest.ActivityId,
                updated.Revision.Value,
                activityUpdateRequest.NewCreatedAt,
                cancellationToken: cancellationToken).EnsureSuccess();
            updated.Revision = result.GetEtagHeader();
        }

        return updated.Revision.Value;
    }

    private async Task<Guid> RestoreDialog(Guid dialogId,
        Guid revision,
        bool disableAltinnEvents,
        CancellationToken cancellationToken)
    {
        var response = await _dialogportenApi
            .Restore(dialogId, revision, disableAltinnEvents, cancellationToken)
            .EnsureSuccess();

        return response.GetEtagHeader();
    }

    private static void PruneExistingImmutableEntities(DialogDto updated, DialogDto? existing)
    {
        if (existing is null) return;

        updated.Transmissions = updated.Transmissions
            .ExceptBy(existing.Transmissions.Select(x => x.Id), x => x.Id)
            .ToList();

        updated.Activities = updated.Activities
            .ExceptBy(existing.Activities.Select(x => x.Id), x => x.Id)
            .ToList();
    }

    private Task UpdateInstanceWithDialogId(SyncInstanceCommand dto, Guid dialogId,
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
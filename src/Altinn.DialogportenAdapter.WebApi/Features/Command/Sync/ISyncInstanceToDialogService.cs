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
    private readonly IInstancesApi _instancesApi;
    private readonly ICachingApplicationsApi _applicationsApi;
    private readonly IDialogportenApi _dialogportenApi;
    private readonly StorageDialogportenDataMerger _dataMerger;
    private readonly ILogger<SyncInstanceToDialogService> _logger;

    public SyncInstanceToDialogService(
        IInstancesApi instancesApi,
        ICachingApplicationsApi applicationsApi,
        IDialogportenApi dialogportenApi,
        StorageDialogportenDataMerger dataMerger,
        ILogger<SyncInstanceToDialogService> logger)
    {
        _instancesApi = instancesApi ?? throw new ArgumentNullException(nameof(instancesApi));
        _applicationsApi = applicationsApi ?? throw new ArgumentNullException(nameof(applicationsApi));
        _dialogportenApi = dialogportenApi ?? throw new ArgumentNullException(nameof(dialogportenApi));
        _dataMerger = dataMerger ?? throw new ArgumentNullException(nameof(dataMerger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Sync(SyncInstanceCommand dto, CancellationToken cancellationToken = default)
    {
        // Create a uuid7 from the instance id and created timestamp to use as dialog id
        var dialogId = dto.InstanceId.ToVersion7(dto.InstanceCreatedAt);

        // Fetch events, application, instance and existing dialog in parallel
        var (existingDialog, application, applicationTexts, instance, events) = await (
            _dialogportenApi.Get(dialogId, cancellationToken).ContentOrDefault(),
            _applicationsApi.GetApplication(dto.AppId, cancellationToken),
            _applicationsApi.GetApplicationTexts(dto.AppId, cancellationToken),
            _instancesApi.GetInstance(dto.PartyId, dto.InstanceId, cancellationToken).ContentOrDefault(),
            _instancesApi.GetInstanceEvents(dto.PartyId, dto.InstanceId, Constants.SupportedEventTypes, cancellationToken).ContentOrDefault()
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

        if (BothIsDeleted(instance, existingDialog))
        {
            return;
        }

        var syncAdapterSettings = application.GetSyncAdapterSettings();
        if (syncAdapterSettings.DisableSync || IsDialogSyncDisabled(instance))
        {
            return;
        }

        if (!syncAdapterSettings.DisableDelete && ShouldPurgeDialog(instance, existingDialog))
        {
            await _dialogportenApi.Purge(
                dialogId,
                existingDialog.Revision!.Value,
                isSilentUpdate: dto.IsMigration,
                cancellationToken: cancellationToken);
            return;
        }

        if (!syncAdapterSettings.DisableDelete && ShouldSoftDeleteDialog(instance, existingDialog))
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

        // Create or update the dialog with the fetched data
        var mergeDto = new MergeDto(dialogId, existingDialog, application, applicationTexts, instance, events, dto.IsMigration);
        var updatedDialog = await _dataMerger.Merge(mergeDto, cancellationToken);
        await UpsertDialog(updatedDialog, syncAdapterSettings, dto.IsMigration, cancellationToken);
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
        if (instance is null)
        {
            return false;
        }

        return instance.DataValues is null
           || !instance.DataValues.TryGetValue(Constants.InstanceDataValueDialogIdKey, out var dialogIdString)
           || !Guid.TryParse(dialogIdString, out var instanceDialogId)
           || instanceDialogId != dialogId;
    }

    private Task UpsertDialog(DialogDto dialog, SyncAdapterSettings settings, bool isMigration, CancellationToken cancellationToken)
    {
        // If the dialog has a revision, it means it is an existing dialog and should be updated.
        if (dialog.Revision.HasValue)
        {
            return _dialogportenApi.Update(dialog, dialog.Revision!.Value,
                isSilentUpdate: isMigration,
                cancellationToken: cancellationToken);
        }

        if (settings.DisableCreate)
        {
            return Task.CompletedTask;
        }

        return _dialogportenApi.Create(dialog, isSilentUpdate: isMigration, cancellationToken: cancellationToken);
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

    private Task UpdateInstanceWithDialogId(SyncInstanceCommand dto, Guid dialogId,
        CancellationToken cancellationToken)
    {
        return _instancesApi.UpdateDataValues(dto.PartyId, dto.InstanceId, new()
        {
            Values = new()
            {
                { Constants.InstanceDataValueDialogIdKey, dialogId.ToString() }
            }
        }, cancellationToken);
    }
}
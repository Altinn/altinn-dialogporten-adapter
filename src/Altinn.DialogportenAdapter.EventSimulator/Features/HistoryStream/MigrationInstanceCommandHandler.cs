using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

namespace Altinn.DialogportenAdapter.EventSimulator.Features.HistoryStream;

internal static partial class MigrationInstanceCommandHandler
{
    public static async Task<SyncInstanceCommand> Handle(
        MigrateInstanceCommand command,
        IStorageApi storageApi,
        ILogger<MigrateInstanceCommand> logger,
        CancellationToken cancellationToken)
    {
        InstanceDto instance;
        try
        {
            instance = await storageApi.GetInstance(command.PartyId, command.InstanceId, cancellationToken);
        }
        catch (Exception e)
        {
            LogFailedToFetchMigrationInstance(logger, e, command.PartyId, command.InstanceId);
            throw;
        }

        try
        {
            return instance.ToSyncInstanceCommand(isMigration: true);
        }
        catch (Exception e)
        {
            LogFailedToCreateMigrationInstanceCommand(
                logger,
                e,
                command.PartyId,
                command.InstanceId,
                instance.Id,
                instance.AppId,
                instance.Created);
            throw;
        }
    }

    [LoggerMessage(
        LogLevel.Error,
        "Failed to fetch migration instance from Storage. PartyId={PartyId}, InstanceId={InstanceId}.")]
    private static partial void LogFailedToFetchMigrationInstance(
        ILogger logger,
        Exception exception,
        string partyId,
        Guid instanceId);

    [LoggerMessage(
        LogLevel.Error,
        "Failed to create migration sync command for Storage instance. PartyId={PartyId}, InstanceId={InstanceId}, StorageInstanceId={StorageInstanceId}, AppId={AppId}, Created={Created}.")]
    private static partial void LogFailedToCreateMigrationInstanceCommand(
        ILogger logger,
        Exception exception,
        string partyId,
        Guid instanceId,
        string storageInstanceId,
        string appId,
        DateTimeOffset created);
}

internal sealed record MigrateInstanceCommand(string PartyId, Guid InstanceId);

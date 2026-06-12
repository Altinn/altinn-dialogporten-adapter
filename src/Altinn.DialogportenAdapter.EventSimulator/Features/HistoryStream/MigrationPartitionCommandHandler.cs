using System.Runtime.CompilerServices;
using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Persistance;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;
using Wolverine.Attributes;

namespace Altinn.DialogportenAdapter.EventSimulator.Features.HistoryStream;

public static partial class MigrationPartitionCommandHandler
{
    public static async Task<(MigrationPartitionEntity, IAsyncEnumerable<InstanceDto>)> Before(
        MigratePartitionCommand command,
        IMigrationPartitionRepository repo,
        IInstanceStreamer instanceStreamer,
        ILogger<MigratePartitionCommand> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var entity = command.IsTest
                ? ToEntity(command)
                : await repo.GetMigrationPartition(command.Partition, command.Organization, cancellationToken) ?? ToEntity(command);

            DateTimeOffset from = command.Partition.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
            DateTimeOffset to = entity.Checkpoint ??= command.Partition.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Local);

            var stream = instanceStreamer.InstanceStream(
                org: command.Organization,
                partyId: command.Party,
                from: from,
                to: to,
                sortOrder: IInstanceStreamer.Order.Descending,
                cancellationToken: cancellationToken);

            return (entity, stream);
        }
        catch (Exception e)
        {
            LogFailedToPrepareMigrationPartition(logger, command, e);
            throw;
        }
    }

    public static async IAsyncEnumerable<SyncInstanceCommand> Handle(
        MigratePartitionCommand command,
        MigrationPartitionEntity entity,
        IAsyncEnumerable<InstanceDto> instanceStream,
        ILogger<MigratePartitionCommand> logger,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (entity.Complete) yield break;

        await using var enumerator = instanceStream.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            InstanceDto instanceDto;
            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    break;
                }

                instanceDto = enumerator.Current;
            }
            catch (Exception e)
            {
                LogFailedToProcessMigrationPartition(logger, command, entity, e);
                throw;
            }

            SyncInstanceCommand syncCommand;
            try
            {
                syncCommand = instanceDto.ToSyncInstanceCommand(isMigration: true);
                entity.InstanceHandled(instanceDto.LastChanged);
            }
            catch (Exception e)
            {
                LogFailedToProcessMigrationPartitionInstance(logger, command, entity, instanceDto, e);
                throw;
            }

            yield return syncCommand;
        }

        entity.Complete = true;
    }

    // There is a chance that the same instance will be counted multiple times due to using lte instead of lt
    // in the instance streamer. However, if we were to use lt we run the risk of missing instances with equal
    // LastChanged timestamps on error recovery. It is better to count instances multiple times, than to skip
    // some of them.
    // We could use the continuation token of the instance api to resume from the last succeeded instance if the
    // instance api were to expose their internal instance ids. That is because the format for the continuation
    // token is $"{lastChanged.Ticks};{id}" url encoded twice, where id is the internal instance id.
    public static Task After(
        MigratePartitionCommand command,
        MigrationPartitionEntity entity,
        IMigrationPartitionRepository repo,
        ILogger<MigratePartitionCommand> logger,
        CancellationToken cancellationToken)
    {
        if (command.IsTest)
        {
            return Task.CompletedTask;
        }

        return UpsertPartition(command, entity, repo, logger, cancellationToken);
    }

    private static MigrationPartitionEntity ToEntity(MigratePartitionCommand item) =>
        new(item.Partition, item.Organization);

    private static async Task UpsertPartition(
        MigratePartitionCommand command,
        MigrationPartitionEntity entity,
        IMigrationPartitionRepository repo,
        ILogger<MigratePartitionCommand> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await repo.Upsert([entity], cancellationToken);
        }
        catch (Exception e)
        {
            LogFailedToSaveMigrationPartitionState(logger, command, entity, e);
            throw;
        }
    }

    private static void LogFailedToPrepareMigrationPartition(
        ILogger logger,
        MigratePartitionCommand command,
        Exception exception) =>
        LogPrepareMigrationPartitionFailure(
            logger,
            exception,
            command.Partition,
            command.Organization,
            FormatParty(command.Party));

    private static void LogFailedToProcessMigrationPartition(
        ILogger logger,
        MigratePartitionCommand command,
        MigrationPartitionEntity entity,
        Exception exception) =>
        LogProcessMigrationPartitionFailure(
            logger,
            exception,
            command.Partition,
            command.Organization,
            FormatParty(command.Party),
            entity.Checkpoint,
            entity.TotalAmount ?? 0,
            entity.Complete);

    private static void LogFailedToProcessMigrationPartitionInstance(
        ILogger logger,
        MigratePartitionCommand command,
        MigrationPartitionEntity entity,
        InstanceDto instance,
        Exception exception) =>
        LogProcessMigrationPartitionInstanceFailure(
            logger,
            exception,
            instance.Id,
            command.Partition,
            command.Organization,
            FormatParty(command.Party),
            instance.AppId,
            instance.LastChanged,
            entity.Checkpoint,
            entity.TotalAmount ?? 0,
            entity.Complete);

    private static void LogFailedToSaveMigrationPartitionState(
        ILogger logger,
        MigratePartitionCommand command,
        MigrationPartitionEntity entity,
        Exception exception) =>
        LogSaveMigrationPartitionStateFailure(
            logger,
            exception,
            command.Partition,
            command.Organization,
            FormatParty(command.Party),
            entity.Checkpoint,
            entity.TotalAmount ?? 0,
            entity.Complete);

    private static string FormatParty(string? party) => party ?? "<all>";

    [LoggerMessage(
        LogLevel.Error,
        "Failed to prepare migration partition {Partition} for organization {Organization}, party {Party}.")]
    private static partial void LogPrepareMigrationPartitionFailure(
        ILogger logger,
        Exception exception,
        DateOnly partition,
        string organization,
        string party);

    [LoggerMessage(
        LogLevel.Error,
        "Failed to process migration partition {Partition} for organization {Organization}, party {Party}. Checkpoint={Checkpoint}, HandledCount={HandledCount}, Complete={Complete}.")]
    private static partial void LogProcessMigrationPartitionFailure(
        ILogger logger,
        Exception exception,
        DateOnly partition,
        string organization,
        string party,
        DateTimeOffset? checkpoint,
        int handledCount,
        bool complete);

    [LoggerMessage(
        LogLevel.Error,
        "Failed to process instance {InstanceId} in migration partition {Partition} for organization {Organization}, party {Party}. InstanceAppId={InstanceAppId}, InstanceLastChanged={InstanceLastChanged}, Checkpoint={Checkpoint}, HandledCount={HandledCount}, Complete={Complete}.")]
    private static partial void LogProcessMigrationPartitionInstanceFailure(
        ILogger logger,
        Exception exception,
        string instanceId,
        DateOnly partition,
        string organization,
        string party,
        string instanceAppId,
        DateTimeOffset instanceLastChanged,
        DateTimeOffset? checkpoint,
        int handledCount,
        bool complete);

    [LoggerMessage(
        LogLevel.Error,
        "Failed to save migration partition state for {Partition} organization {Organization}, party {Party}. Checkpoint={Checkpoint}, HandledCount={HandledCount}, Complete={Complete}.")]
    private static partial void LogSaveMigrationPartitionStateFailure(
        ILogger logger,
        Exception exception,
        DateOnly partition,
        string organization,
        string party,
        DateTimeOffset? checkpoint,
        int handledCount,
        bool complete);
}

[MessageTimeout(60*60*2)] // 2 hours to handle very large partitions
public sealed record MigratePartitionCommand(DateOnly Partition, string Organization, string? Party)
{
    public bool IsTest => Party is not null;
}

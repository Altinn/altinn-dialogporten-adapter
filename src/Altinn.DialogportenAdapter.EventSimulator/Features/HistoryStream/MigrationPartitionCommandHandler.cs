using System.Runtime.CompilerServices;
using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Persistance;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

namespace Altinn.DialogportenAdapter.EventSimulator.Features.HistoryStream;

public static class MigrationPartitionCommandHandler
{
    public static async Task<(MigrationPartitionEntity, IAsyncEnumerable<InstanceDto>)> Before(
        MigratePartitionCommand command,
        IMigrationPartitionRepository repo,
        IInstanceStreamer instanceStreamer,
        CancellationToken cancellationToken)
    {
        var entity = command.IsTest
            ? ToEntity(command)
            : await repo.Get(command.Partition, command.Organization, cancellationToken) ?? ToEntity(command);

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

    public static async IAsyncEnumerable<SyncInstanceCommand> Handle(
        MigratePartitionCommand _,
        MigrationPartitionEntity entity,
        IAsyncEnumerable<InstanceDto> instanceStream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (entity.Complete) yield break;

        await foreach (var instanceDto in instanceStream.WithCancellation(cancellationToken))
        {
            yield return instanceDto.ToSyncInstanceCommand(isMigration: true);
            entity.InstanceHandled(instanceDto.LastChanged);
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
        CancellationToken cancellationToken) =>
        command.IsTest
            ? Task.CompletedTask
            : repo.Upsert([entity], cancellationToken);

    private static MigrationPartitionEntity ToEntity(MigratePartitionCommand item) =>
        new(item.Partition, item.Organization);
}

public sealed record MigratePartitionCommand(DateOnly Partition, string Organization, string? Party)
{
    public bool IsTest => Party is not null;
}
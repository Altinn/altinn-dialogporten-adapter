using Altinn.DialogportenAdapter.EventSimulator.Common.Channels;
using Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace Altinn.DialogportenAdapter.EventSimulator.Features.HistoryStream;

internal sealed class MigrationPartitionCommandConsumer : IChannelConsumer<MigrationPartitionCommand>
{
    private static readonly AsyncRetryPolicy RetryPolicy = Policy
        .Handle<Exception>(x => x is not OperationCanceledException)
        .WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 5));

    private readonly IChannelPublisher<InstanceEvent> _publisher;
    private readonly MigrationPartitionRepository _migrationPartitionRepository;
    private readonly InstanceStreamer _instanceStreamer;

    public MigrationPartitionCommandConsumer(
        IChannelPublisher<InstanceEvent> publisher,
        InstanceStreamer instanceStreamer,
        MigrationPartitionRepository migrationPartitionRepository)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _instanceStreamer = instanceStreamer ?? throw new ArgumentNullException(nameof(instanceStreamer));
        _migrationPartitionRepository = migrationPartitionRepository ?? throw new ArgumentNullException(nameof(migrationPartitionRepository));
    }

    public async Task Consume(MigrationPartitionCommand command, int taskNumber, CancellationToken cancellationToken)
    {
        var entity = !command.IsTest
            ? await _migrationPartitionRepository.Get(command.Partition, command.Organization, cancellationToken) ?? ToEntity(command)
            : ToEntity(command);

        if (entity.Complete)
        {
            return;
        }

        DateTimeOffset from = command.Partition.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        entity.Checkpoint ??= command.Partition.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Local);
        try
        {
            await RetryPolicy.ExecuteAsync(async cancellationToken =>
            {
                await foreach (var instanceDto in _instanceStreamer.InstanceStream(
                                   org: command.Organization,
                                   partyId: command.Party,
                                   from: from,
                                   to: entity.Checkpoint,
                                   sortOrder: InstanceStreamer.Order.Descending,
                                   cancellationToken: cancellationToken))
                {
                    await _publisher.Publish(instanceDto.ToInstanceEvent(isMigration: true), cancellationToken);
                    entity.InstanceHandled(instanceDto.LastChanged);
                }
            }, cancellationToken);
        }
        catch (Exception)
        {
            await SaveCheckpoint(command, entity, cancellationToken);
            throw;
        }

        await MarkAsComplete(command, entity, cancellationToken);
    }

    private Task MarkAsComplete(
        MigrationPartitionCommand command,
        MigrationPartitionEntity entity,
        CancellationToken cancellationToken)
    {
        entity.Complete = true;
        return command.IsTest
            ? Task.CompletedTask
            : _migrationPartitionRepository.Upsert([entity], cancellationToken);
    }

    private Task SaveCheckpoint(
        MigrationPartitionCommand command,
        MigrationPartitionEntity entity,
        CancellationToken cancellationToken)
    {
        // There is a chance that the same instance will be counted multiple times due to using lte instead of lt
        // in the instance streamer. However, if we were to use lt we run the risk of missing instances with equal
        // LastChanged timestamps on error recovery. It is better to count instances multiple times, than to skip
        // some of them.
        // We could use the continuation token of the instance api to resume from the last succeeded instance if the
        // instance api were to expose their internal instance ids. That is because the format for the continuation
        // token is $"{lastChanged.Ticks};{id}" url encoded twice, where id is the internal instance id.
        return command.IsTest
            ? Task.CompletedTask
            : _migrationPartitionRepository.Upsert([entity], cancellationToken);
    }

    private static MigrationPartitionEntity ToEntity(MigrationPartitionCommand item) =>
        new(item.Partition, item.Organization);
}

internal sealed record MigrationPartitionCommand(DateOnly Partition, string Organization, string? Party)
{
    public bool IsTest => Party is not null;
}
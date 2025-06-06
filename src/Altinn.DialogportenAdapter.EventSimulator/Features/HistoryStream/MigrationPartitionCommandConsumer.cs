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

    public async Task Consume(MigrationPartitionCommand item, int taskNumber, CancellationToken cancellationToken)
    {
        var counter = 0;
        var from = (DateTimeOffset)item.Partition.ToDateTime(TimeOnly.MinValue);
        var to = (DateTimeOffset)item.Partition.ToDateTime(TimeOnly.MaxValue);
        await RetryPolicy.ExecuteAsync(async cancellationToken =>
        {
            await foreach (var instanceDto in _instanceStreamer.InstanceStream(
                               org: item.Organization,
                               partyId: item.Party,
                               from: from,
                               to: to,
                               sortOrder: InstanceStreamer.Order.Descending,
                               cancellationToken: cancellationToken))
            {
                await _publisher.Publish(instanceDto.ToInstanceEvent(isMigration: true), cancellationToken);
                counter++;
                to = instanceDto.LastChanged < to
                    ? instanceDto.LastChanged
                    : to;
            }
        }, cancellationToken);

        if (!item.IsTest)
        {
            await _migrationPartitionRepository.Upsert(
                [new(item.Partition, item.Organization) { TotalAmount = counter }],
                cancellationToken);
        }
    }
}

internal sealed record MigrationPartitionCommand(DateOnly Partition, string Organization, string? Party)
{
    public bool IsTest => Party is not null;
}
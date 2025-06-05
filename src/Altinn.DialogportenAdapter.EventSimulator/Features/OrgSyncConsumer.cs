using Altinn.DialogportenAdapter.EventSimulator.Common.Channels;
using Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;
using Altinn.DialogportenAdapter.EventSimulator.Features.Migration;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

namespace Altinn.DialogportenAdapter.EventSimulator.Features;

internal sealed record OrgSyncEvent(string Org, DateTimeOffset? From, DateTimeOffset? To);

internal sealed class OrgSyncConsumer : IChannelConsumer<OrgSyncEvent>
{
    private readonly IChannelPublisher<InstanceEvent> _publisher;
    private readonly InstanceStreamer _instanceStreamer;
    private readonly ILogger _logger;

    public OrgSyncConsumer(ILogger<OrgSyncConsumer> logger,
        InstanceStreamer instanceStreamer,
        IChannelPublisher<InstanceEvent> publisher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _instanceStreamer = instanceStreamer ?? throw new ArgumentNullException(nameof(instanceStreamer));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public async Task Consume(OrgSyncEvent item, int taskNumber, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{TaskNumber}: Consuming {@OrgSyncEvent}", taskNumber, item);
        var to = item.To ?? DateTimeOffset.MaxValue;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var instanceDto in _instanceStreamer.InstanceStream(
                                   org: item.Org,
                                   from: item.From,
                                   to: to,
                                   sortOrder: InstanceStreamer.Order.Descending,
                                   cancellationToken: cancellationToken))
                {
                    await _publisher.Publish(instanceDto.ToInstanceEvent(isMigration: true), cancellationToken);
                    to = instanceDto.LastChanged < to ? instanceDto.LastChanged : to;
                }
            }
            catch (Exception e) when (e is TaskCanceledException or OperationCanceledException) { /* Swallow by design */ }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while consuming instance history stream for {org}. Attempting to reset stream in 5 seconds.", item.Org);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                continue;
            }

            return;
        }
    }
}

internal sealed class FakeMigrationPartitionCommandConsumer : IChannelConsumer<MigrationPartitionCommand>
{
    public Task Consume(MigrationPartitionCommand item, int taskNumber, CancellationToken cancellationToken)
    {
        Console.WriteLine($"{item.Organization} {item.Partition}");
        return Task.CompletedTask;
    }
}

internal sealed class MigrationPartitionCommandConsumer : IChannelConsumer<MigrationPartitionCommand>
{
    private readonly IChannelPublisher<InstanceEvent> _publisher;
    private readonly InstanceStreamer _instanceStreamer;

    public MigrationPartitionCommandConsumer(IChannelPublisher<InstanceEvent> publisher, InstanceStreamer instanceStreamer)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _instanceStreamer = instanceStreamer ?? throw new ArgumentNullException(nameof(instanceStreamer));
    }


    public async Task Consume(MigrationPartitionCommand item, int taskNumber, CancellationToken cancellationToken)
    {
        var from = item.Partition.ToDateTime(TimeOnly.MinValue);
        var to = item.Partition.ToDateTime(TimeOnly.MaxValue);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var instanceDto in _instanceStreamer.InstanceStream(
                                   org: item.Organization,
                                   partyIds: item.Parties,
                                   from: from,
                                   to: to,
                                   sortOrder: InstanceStreamer.Order.Descending,
                                   cancellationToken: cancellationToken))
                {
                    // TODO: FORTSETT HER!
                    await _publisher.Publish(instanceDto.ToInstanceEvent(isMigration: true), cancellationToken);
                    // to = instanceDto.LastChanged < to ? instanceDto.LastChanged : to;
                }
            }
            catch (Exception e) when (e is TaskCanceledException or OperationCanceledException) { /* Swallow by design */ }
            catch (Exception e)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                continue;
            }

            return;
        }
    }
}

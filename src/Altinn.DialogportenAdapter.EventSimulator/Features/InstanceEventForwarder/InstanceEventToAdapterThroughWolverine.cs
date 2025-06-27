using Altinn.DialogportenAdapter.EventSimulator.Common.Channels;
using Altinn.Storage.Contracts;
using Wolverine;

namespace Altinn.DialogportenAdapter.EventSimulator.Features.InstanceEventForwarder;

internal sealed class InstanceEventToAdapterThroughWolverine(IMessageBus messageBus)
    : IChannelConsumer<InstanceUpdatedEvent>
{
    private readonly IMessageBus _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));

    public Task Consume(InstanceUpdatedEvent item, int taskNumber, CancellationToken cancellationToken) =>
        _messageBus.PublishAsync(item).AsTask();
}
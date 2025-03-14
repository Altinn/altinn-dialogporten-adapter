namespace Altinn.DialogportenAdapter.EventSimulator.Common.Channels;

internal interface IChannelConsumer<in T>
{
    Task Consume(T item, int taskNumber, CancellationToken cancellationToken);
}
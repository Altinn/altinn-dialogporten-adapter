namespace Altinn.DialogportenAdapter.EventSimulator.Common.Channels;

internal interface IChannelPublisher<in T>
{
    ValueTask Publish(T instanceEvent, CancellationToken cancellationToken);
    bool TryPublish(T instanceEvent);
}
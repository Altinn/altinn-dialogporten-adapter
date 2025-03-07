using System.Collections.Concurrent;
using System.Threading.Channels;
using Altinn.DialogportenAdapter.EventSimulator.Common;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

namespace Altinn.DialogportenAdapter.EventSimulator;

internal sealed class EventStreamer
{
    private readonly IStorageApi _storageApi;
    private readonly InstanceEventStreamer _instanceEventStreamer;
    private readonly ILogger<EventStreamer> _logger;
    private readonly IChannelPublisher<InstanceEvent> _instanceEventPublisher;

    public EventStreamer(InstanceEventStreamer instanceEventStreamer,
        ILogger<EventStreamer> logger, 
        IChannelPublisher<InstanceEvent> instanceEventPublisher, 
        IStorageApi storageApi)
    {
        _instanceEventStreamer = instanceEventStreamer ?? throw new ArgumentNullException(nameof(instanceEventStreamer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _instanceEventPublisher = instanceEventPublisher ?? throw new ArgumentNullException(nameof(instanceEventPublisher));
        _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
    }

    public async Task StreamEvents(
        int numberOfProducers, 
        CancellationToken cancellationToken)
    {
        await ProduceEvents(numberOfProducers, cancellationToken);
    }
    
    private async Task ProduceEvents(int producers, CancellationToken cancellationToken)
    {
        var appQueue = new ConcurrentQueue<string>(await GetAllAppIds(cancellationToken));
        await Task.WhenAll(Enumerable.Range(1, producers).Select(Produce));
        return;

        async Task Produce(int taskNumber)
        {
            while (appQueue.TryDequeue(out var appId))
            {
                await foreach (var instanceEvent in _instanceEventStreamer.GetInstanceStream(appId, cancellationToken))
                {
                    _logger.LogInformation("{TaskNumber}: Producing event for {instanceId}", taskNumber, instanceEvent.InstanceId);
                    await _instanceEventPublisher.Publish(instanceEvent, cancellationToken);
                }
            }
        }
    }
    
    private async Task<List<string>> GetAllAppIds(CancellationToken cancellationToken)
    {
        var apps = await _storageApi.GetApplications(cancellationToken);
        return apps.Applications
            .Select(x => x.Id)
            .ToList();
    }
}

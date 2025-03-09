using System.Collections.Concurrent;
using Altinn.DialogportenAdapter.EventSimulator.Common;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

namespace Altinn.DialogportenAdapter.EventSimulator;

internal sealed class EventStreamer
{
    private readonly IStorageApi _storageApi;
    private readonly InstanceStreamer _instanceStreamer;
    private readonly ILogger<EventStreamer> _logger;
    private readonly IChannelPublisher<InstanceEvent> _instanceEventPublisher;

    public EventStreamer(InstanceStreamer instanceStreamer,
        ILogger<EventStreamer> logger, 
        IChannelPublisher<InstanceEvent> instanceEventPublisher, 
        IStorageApi storageApi)
    {
        _instanceStreamer = instanceStreamer ?? throw new ArgumentNullException(nameof(instanceStreamer));
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
                await foreach (var instanceDto in _instanceStreamer.InstanceHistoryStream(appId, cancellationToken))
                {
                    _logger.LogInformation("{TaskNumber}: Producing event for {instanceId}", taskNumber, instanceDto.Id);
                    
                    await _instanceEventPublisher.Publish(instanceDto.ToInstanceEvent(), cancellationToken);
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

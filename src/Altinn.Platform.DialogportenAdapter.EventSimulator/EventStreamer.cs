using System.Collections.Concurrent;
using System.Threading.Channels;
using Altinn.Platform.DialogportenAdapter.EventSimulator.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.DialogportenAdapter.EventSimulator;

internal sealed class EventStreamer
{
    private readonly IApplicationService _applicationService;
    private readonly InstanceStreamHttpClient _instanceStreamHttpClient;
    private readonly IStorageAdapterApi _storageAdapterApi;
    private readonly ILogger<EventStreamer> _logger;

    public EventStreamer(IApplicationService applicationService,
        InstanceStreamHttpClient instanceStreamHttpClient,
        IStorageAdapterApi storageAdapterApi, 
        ILogger<EventStreamer> logger)
    {
        _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
        _instanceStreamHttpClient = instanceStreamHttpClient ?? throw new ArgumentNullException(nameof(instanceStreamHttpClient));
        _storageAdapterApi = storageAdapterApi ?? throw new ArgumentNullException(nameof(storageAdapterApi));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StreamEvents(
        int numberOfProducers, 
        int numberOfConsumers, 
        int cacheCapacity, 
        CancellationToken cancellationToken)
    {
        var instanceEventChannel = Channel.CreateBounded<InstanceEvent>(cacheCapacity);
        var producerTask = ProduceEvents(numberOfProducers, instanceEventChannel, cancellationToken);
        var consumerTask = ConsumeEvents(numberOfConsumers, instanceEventChannel, cancellationToken);
        await Task.WhenAll(producerTask, consumerTask);
    }
    
    private async Task ProduceEvents(int producers, ChannelWriter<InstanceEvent> writer, CancellationToken cancellationToken)
    {
        var appQueue = new ConcurrentQueue<IStorageApplicationContext>(await _applicationService.GetApplicationInfo(cancellationToken));
        await Task.WhenAll(Enumerable.Range(1, producers).Select(Produce));
        writer.Complete();
        return;

        async Task Produce(int taskNumber)
        {
            while (appQueue.TryDequeue(out var appContext))
            {
                var token = await appContext.GetToken(cancellationToken);
                await foreach (var instanceEvent in _instanceStreamHttpClient.GetInstanceStream(appContext.AppId, token, cancellationToken))
                {
                    _logger.LogInformation("{TaskNumber}: Producing event for {instanceId}", taskNumber, instanceEvent.InstanceId);
                    await writer.WriteAsync(instanceEvent, cancellationToken);
                }
            }
        }
    }
    
    private async Task ConsumeEvents(int consumers, ChannelReader<InstanceEvent> reader, CancellationToken cancellationToken)
    {
        await Task.WhenAll(Enumerable.Range(1, consumers).Select(Consume));
        return;
        
        async Task Consume(int taskNumber)
        {
            await foreach (var instanceEvent in reader.ReadAllAsync(cancellationToken))
            {
                _logger.LogInformation("{TaskNumber}: Consuming event for {instanceId}", taskNumber, instanceEvent.InstanceId);
                await _storageAdapterApi.Sync(instanceEvent, cancellationToken);
            }
        }
    }
}

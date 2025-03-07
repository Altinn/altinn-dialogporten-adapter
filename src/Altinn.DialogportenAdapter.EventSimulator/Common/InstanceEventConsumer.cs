using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

namespace Altinn.DialogportenAdapter.EventSimulator.Common;

internal sealed class InstanceEventConsumer : IChannelConsumer<InstanceEvent>
{
    private readonly IStorageAdapterApi _storageAdapterApi;
    private readonly ILogger _logger;
    
    public InstanceEventConsumer(IStorageAdapterApi storageAdapterApi, ILogger<InstanceEventConsumer> logger)
    {
        _storageAdapterApi = storageAdapterApi ?? throw new ArgumentNullException(nameof(storageAdapterApi));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(InstanceEvent item, int taskNumber, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{TaskNumber}: Consuming event for {instanceId}", taskNumber, item.InstanceId);
        await _storageAdapterApi.Sync(item, cancellationToken);
    }
}
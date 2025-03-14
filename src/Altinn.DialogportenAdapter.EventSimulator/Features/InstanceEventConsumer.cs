using Altinn.DialogportenAdapter.EventSimulator.Common;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

namespace Altinn.DialogportenAdapter.EventSimulator.Features;

internal sealed class InstanceEventConsumer : IChannelConsumer<InstanceEvent>
{
    private readonly IStorageAdapterApi _storageAdapterApi;
    private readonly ILogger<InstanceEventConsumer>  _logger;
    
    public InstanceEventConsumer(
        IStorageAdapterApi storageAdapterApi, 
        ILogger<InstanceEventConsumer> logger)
    {
        _storageAdapterApi = storageAdapterApi ?? throw new ArgumentNullException(nameof(storageAdapterApi));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(InstanceEvent item, int taskNumber, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{TaskNumber}: Consuming {@InstanceEvent}", taskNumber, item);
        await _storageAdapterApi.Sync(item, cancellationToken);
    }
}

internal sealed class NullConsumer<T> : IChannelConsumer<T>
{
    private readonly ILogger<T> _logger;
    
    public NullConsumer(ILogger<T> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public Task Consume(T item, int taskNumber, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{TaskNumber}: Consuming {@InstanceEvent}", taskNumber, item);
        return Task.CompletedTask;
    }
}
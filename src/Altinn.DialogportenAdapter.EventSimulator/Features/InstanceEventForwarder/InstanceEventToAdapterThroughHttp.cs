using Altinn.DialogportenAdapter.EventSimulator.Common.Channels;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;
using Altinn.Storage.Contracts;

namespace Altinn.DialogportenAdapter.EventSimulator.Features.InstanceEventForwarder;

internal sealed class InstanceEventToAdapterThroughHttp : IChannelConsumer<InstanceUpdatedEvent>
{
    private readonly IStorageAdapterApi _storageAdapterApi;
    private readonly ILogger<InstanceEventToAdapterThroughHttp>  _logger;

    public InstanceEventToAdapterThroughHttp(
        IStorageAdapterApi storageAdapterApi,
        ILogger<InstanceEventToAdapterThroughHttp> logger)
    {
        _storageAdapterApi = storageAdapterApi ?? throw new ArgumentNullException(nameof(storageAdapterApi));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(InstanceUpdatedEvent item, int taskNumber, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{TaskNumber}: Consuming {@InstanceEvent}", taskNumber, item);
        await _storageAdapterApi.Sync(item, cancellationToken);
    }
}
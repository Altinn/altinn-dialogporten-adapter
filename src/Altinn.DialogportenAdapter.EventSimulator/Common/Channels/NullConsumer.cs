namespace Altinn.DialogportenAdapter.EventSimulator.Common.Channels;

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
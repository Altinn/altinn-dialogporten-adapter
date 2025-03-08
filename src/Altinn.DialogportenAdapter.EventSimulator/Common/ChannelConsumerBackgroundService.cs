using System.Threading.Channels;

namespace Altinn.DialogportenAdapter.EventSimulator.Common;

internal sealed class ChannelConsumerBackgroundService<TConsumer, TEvent> : BackgroundService, IChannelPublisher<TEvent>
    where TConsumer : IChannelConsumer<TEvent>
{
    private readonly Channel<TEvent> _channel;
    private readonly int _consumers;
    private readonly ILogger<TConsumer> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ChannelConsumerBackgroundService(ILogger<TConsumer> logger,
        IServiceScopeFactory serviceScopeFactory,
        int consumers = 1,
        int? capacity = 10)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _consumers = consumers > 0 ? consumers : throw new ArgumentOutOfRangeException(nameof(consumers));
        _channel = capacity.HasValue
            ? Channel.CreateBounded<TEvent>(capacity.Value)
            : Channel.CreateUnbounded<TEvent>();
    }
    
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        List<Task> consumerTasks = [];
        for (var taskNumber = 1; taskNumber <= _consumers; taskNumber++)
        {
            consumerTasks.Add(Consume(taskNumber, cancellationToken));
        }
        await Task.WhenAll(consumerTasks);
    }
    
    private async Task Consume(int taskNumber, CancellationToken cancellationToken)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var consumer = scope.ServiceProvider.GetRequiredService<IChannelConsumer<TEvent>>();
                await consumer.Consume(item, taskNumber, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "{TaskNumber}: Failed to consume item.", taskNumber);
            }
        }
    }
    
    public ValueTask Publish(TEvent instanceEvent, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(instanceEvent, cancellationToken);
    }
}

internal interface IChannelPublisher<in T>
{
    ValueTask Publish(T instanceEvent, CancellationToken cancellationToken);
}

internal interface IChannelConsumer<in T>
{
    Task Consume(T item, int taskNumber, CancellationToken cancellationToken);
}

internal static class ChannelConsumerExtensions
{
    public static IServiceCollection AddChannelConsumer<TConsumer, TEvent>(
        this IServiceCollection services,
        int consumers = 1,
        int? capacity = 10)
        where TConsumer : class, IChannelConsumer<TEvent>
    {
        services.AddSingleton<ChannelConsumerBackgroundService<TConsumer, TEvent>>(x => 
            ActivatorUtilities.CreateInstance<ChannelConsumerBackgroundService<TConsumer, TEvent>>(x, consumers, capacity!));
        services.AddHostedService<ChannelConsumerBackgroundService<TConsumer, TEvent>>(x => x.GetRequiredService<ChannelConsumerBackgroundService<TConsumer, TEvent>>());
        services.AddTransient<IChannelConsumer<TEvent>, TConsumer>();
        services.AddSingleton<IChannelPublisher<TEvent>>(x => x.GetRequiredService<ChannelConsumerBackgroundService<TConsumer, TEvent>>());
        return services;
    }
}
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Altinn.DialogportenAdapter.EventSimulator.Common.Channels;

internal static class ChannelConsumerExtensions
{
    public static IServiceCollection AddChannelConsumer<TConsumer, TEvent>(
        this IServiceCollection services,
        int consumers = 1,
        int? capacity = 10)
        where TConsumer : class, IChannelConsumer<TEvent>
    {
        var backgroundServiceExists = services.Any(x => x.ServiceType == typeof(ChannelConsumerBackgroundService<TConsumer, TEvent>));
        if (!backgroundServiceExists)
        {
            services.AddSingleton<ChannelConsumerBackgroundService<TConsumer, TEvent>>(x => 
                ActivatorUtilities.CreateInstance<ChannelConsumerBackgroundService<TConsumer, TEvent>>(x, consumers, capacity!));
            services.AddSingleton<IChannelPublisher<TEvent>>(x => x.GetRequiredService<ChannelConsumerBackgroundService<TConsumer, TEvent>>());
            services.AddHostedService<ChannelConsumerBackgroundService<TConsumer, TEvent>>(x => x.GetRequiredService<ChannelConsumerBackgroundService<TConsumer, TEvent>>());
        }
        services.TryAddEnumerable(ServiceDescriptor.Transient<IChannelConsumer<TEvent>, TConsumer>());
        return services;
    }
}
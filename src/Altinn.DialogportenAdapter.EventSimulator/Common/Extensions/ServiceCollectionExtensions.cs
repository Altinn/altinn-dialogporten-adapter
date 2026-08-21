using Altinn.DialogportenAdapter.EventSimulator.Common.StartupLoaders;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Persistance;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Resources;

namespace Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;

internal static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder ReplaceLocalDevelopmentResources(this IHostApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment() ||
            !builder.Configuration.TryGetLocalDevelopmentSettings(out var opt))
        {
            return builder;
        }

        builder.Services
            .DoIf(opt.DisableAzureStorage, x => x.Replace<IMigrationPartitionRepository, NullMigrationPartitionRepository>(ServiceLifetime.Transient))
            .DoIf(opt.DisableAzureStorage, x => x.RemoveAllImplementationTypes(typeof(AzureTableStartupLoader)));

        return builder;
    }

    private static IServiceCollection Replace<TService, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime)
        where TService : class
        where TImplementation : class, TService
    {
        var serviceType = typeof(TService);
        var implementationType = typeof(TImplementation);
        // Remove all matching service registrations
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == serviceType) services.RemoveAt(i);
        }

        services.Add(ServiceDescriptor.Describe(serviceType, implementationType, lifetime));
        return services;
    }

    private static IServiceCollection DoIf(this IServiceCollection services, bool predicate, Action<IServiceCollection> action)
    {
        if (predicate) action(services);
        return services;
    }

    private static IServiceCollection RemoveAllImplementationTypes(this IServiceCollection collection, Type implementationType)
    {
        ArgumentNullException.ThrowIfNull(implementationType);

        for (var i = collection.Count - 1; i >= 0; i--)
        {
            if (collection[i].ImplementationType == implementationType) collection.RemoveAt(i);
        }

        return collection;
    }

    public static IServiceCollection ConfigureTelemetry(this IServiceCollection services, Settings settings)
    {
        if (string.IsNullOrEmpty(settings.ApplicationInsights.ConnectionString))
        {
            throw new ArgumentException("ApplicationInsights connection string is null or empty");
        }
        services
            .AddOpenTelemetry()
            .ConfigureResource(x => x.AddAttributes([
                new("service.name", "platform-dialogporten-eventsimulator")
            ]))
            .UseAzureMonitor(x =>
            {
                x.ConnectionString = settings.ApplicationInsights.ConnectionString;
                x.SamplingRatio = 0.05F;
                x.EnableLiveMetrics = false;
                x.StorageDirectory = "/tmp/logtelemetry";
            });

        return services;
    }
}

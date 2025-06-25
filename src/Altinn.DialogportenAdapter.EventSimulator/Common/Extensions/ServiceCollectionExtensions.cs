using Altinn.DialogportenAdapter.EventSimulator.Common.StartupLoaders;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

namespace Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection ReplaceTransient<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService =>
        services.Replace<TService, TImplementation>(ServiceLifetime.Transient);

    public static IServiceCollection ReplaceScoped<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService =>
        services.Replace<TService, TImplementation>(ServiceLifetime.Scoped);

    public static IServiceCollection ReplaceSingleton<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService =>
        services.Replace<TService, TImplementation>(ServiceLifetime.Singleton);

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

    public static IHostApplicationBuilder ReplaceLocalDevelopmentResources(this IHostApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment() ||
            !builder.Configuration.TryGetLocalDevelopmentSettings(out var opt))
        {
            return builder;
        }

        builder.Services
            .DoIf(opt.DisableAzureStorage, x => x.ReplaceTransient<IMigrationPartitionRepository, MockMigrationPartitionRepository>())
            .DoIf(opt.DisableAzureStorage, x => x.RemoveAllImplementationTypes(typeof(AzureTableStartupLoader)));

        return builder;
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
}
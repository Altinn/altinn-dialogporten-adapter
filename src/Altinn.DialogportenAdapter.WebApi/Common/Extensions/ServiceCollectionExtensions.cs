using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Microsoft.AspNetCore.Authorization;

namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection ReplaceTransient<TService, TImplementation>(
        this IServiceCollection services,
        bool predicate = true)
        where TService : class
        where TImplementation : class, TService =>
        services.Replace<TService, TImplementation>(ServiceLifetime.Transient, predicate);

    public static IServiceCollection ReplaceScoped<TService, TImplementation>(
        this IServiceCollection services,
        bool predicate = true)
        where TService : class
        where TImplementation : class, TService =>
        services.Replace<TService, TImplementation>(ServiceLifetime.Scoped, predicate);

    public static IServiceCollection ReplaceSingleton<TService, TImplementation>(
        this IServiceCollection services,
        bool predicate = true)
        where TService : class
        where TImplementation : class, TService =>
        services.Replace<TService, TImplementation>(ServiceLifetime.Singleton, predicate);

    private static IServiceCollection Replace<TService, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        bool predicate = true)
        where TService : class
        where TImplementation : class, TService
    {
        if (!predicate)
        {
            return services;
        }

        var serviceType = typeof(TService);
        var implementationType = typeof(TImplementation);

        // Remove all matching service registrations
        foreach (var descriptor in services
            .Where(x => x.ServiceType == serviceType)
            .ToList())
        {
            services.Remove(descriptor);
        }

        services.Add(ServiceDescriptor.Describe(serviceType, implementationType, lifetime));

        return services;
    }

    public static IServiceCollection AddHostedService<THostedService>(this IServiceCollection services, bool predicate)
        where THostedService : class, IHostedService =>
        predicate
            ? services.AddHostedService<THostedService>()
            : services;

    public static IHostApplicationBuilder ReplaceLocalDevelopmentResources(this IHostApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment() ||
            !builder.Configuration.TryGetLocalDevelopmentSettings(out var localDevelopmentSettings))
        {
            return builder;
        }

        builder.Services
            .ReplaceTransient<IDialogportenApi, MockDialogportenApi>(predicate: localDevelopmentSettings.MockDialogportenApi)
            .ReplaceSingleton<IAuthorizationHandler, AllowAnonymousHandler>(predicate: localDevelopmentSettings.DisableAuth);

        return builder;
    }
}
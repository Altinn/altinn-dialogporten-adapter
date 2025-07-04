using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Microsoft.AspNetCore.Authorization;

namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

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
            .DoIf(opt.MockDialogportenApi, x => x.Replace<IDialogportenApi, MockDialogportenApi>(ServiceLifetime.Transient))
            .DoIf(opt.DisableAuth, x => x.Replace<IAuthorizationHandler, AllowAnonymousHandler>(ServiceLifetime.Singleton));

        return builder;
    }

    private static IServiceCollection DoIf(this IServiceCollection services, bool predicate, Action<IServiceCollection> action)
    {
        if (predicate) action(services);
        return services;
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
}
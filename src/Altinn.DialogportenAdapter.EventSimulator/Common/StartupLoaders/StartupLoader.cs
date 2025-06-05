using System.Reflection;

namespace Altinn.DialogportenAdapter.EventSimulator.Common.StartupLoaders;

internal sealed class StartupLoader : IHostedService
{
    private readonly IEnumerable<IStartupLoader> _loaders;

    public StartupLoader(IEnumerable<IStartupLoader> loaders)
    {
        _loaders = loaders ?? throw new ArgumentNullException(nameof(loaders));
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(_loaders.Select(x => x.Load(cancellationToken)));

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static class StartupLoaderExtensions
{
    public static IServiceCollection AddStartupLoaders(this IServiceCollection services)
    {
        var startupLoaderType = typeof(IStartupLoader);
        var loaders = Assembly
            .GetExecutingAssembly()
            .DefinedTypes
            .Where(x => !x.IsInterface && !x.IsAbstract && x.IsAssignableTo(startupLoaderType));

        foreach (var loader in loaders)
        {
            services.Add(ServiceDescriptor.Transient(startupLoaderType, loader));
        }

        services.AddHostedService<StartupLoader>();

        return services;
    }
}

internal interface IStartupLoader
{
    Task Load(CancellationToken cancellationToken);
}
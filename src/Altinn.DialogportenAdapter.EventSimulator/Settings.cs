using System.Diagnostics.CodeAnalysis;
using Altinn.ApiClients.Maskinporten.Config;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Persistance;

namespace Altinn.DialogportenAdapter.EventSimulator;

public sealed record Settings(
    DialogportenAdapterSettings DialogportenAdapter,
    WolverineSettings WolverineSettings);

public sealed record WolverineSettings(
    string ServiceBusConnectionString,
    string? ManagementConnectionString = null,
    int ListenerCount = 50);

public sealed record DialogportenAdapterSettings(
    MaskinportenSettings Maskinporten,
    AltinnPlatformSettings Altinn,
    AdapterSettings Adapter,
    AzureStorageSettings AzureStorage,
    EventSimulatorSettings EventSimulator);

public sealed record EventSimulatorSettings(bool EnableUpdateStream = false);

public record AzureStorageSettings(string ConnectionString)
{
    public static string GetTableName(IHostEnvironment hostEnvironment) =>
        $"{hostEnvironment.EnvironmentName}{nameof(MigrationPartitionEntity)}";
}

public record AdapterSettings(Uri InternalBaseUri);

public sealed record AltinnPlatformSettings(Uri ApiStorageEndpoint);

public record KeyVaultSettings(string ClientId, string ClientSecret, string TenantId, string SecretUri);

internal sealed record LocalDevelopmentSettings(bool DisableAzureStorage)
{
    public const string ConfigurationSectionName = "LocalDevelopment";
}

internal static class LocalDevelopmentExtensions
{
    public static bool TryGetLocalDevelopmentSettings(this IConfiguration configuration, [NotNullWhen(true)] out LocalDevelopmentSettings? settings)
    {
        settings = configuration
            .GetSection(LocalDevelopmentSettings.ConfigurationSectionName)
            .Get<LocalDevelopmentSettings>();
        return settings is not null;
    }

    public static IConfigurationBuilder AddLocalDevelopmentSettings(this IConfigurationBuilder config, IHostEnvironment hostingEnvironment)
    {
        const string localAppsettingsJsonFileName = "appsettings.local.json";
        if (!hostingEnvironment.IsDevelopment())
        {
            return config;
        }

        config.AddJsonFile(localAppsettingsJsonFileName, optional: true, reloadOnChange: true);
        return config;
    }
}

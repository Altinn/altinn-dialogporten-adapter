using System.Diagnostics.CodeAnalysis;
using Altinn.ApiClients.Maskinporten.Config;

namespace Altinn.DialogportenAdapter.WebApi;

public sealed record Settings(
    DialogportenAdapterSettings DialogportenAdapter,
    WolverineSettings WolverineSettings);

public sealed record WolverineSettings(string ServiceBusConnectionString, int ListenerCount = 50);

public sealed record DialogportenAdapterSettings(
    MaskinportenSettings Maskinporten,
    AltinnPlatformSettings Altinn,
    DialogportenSettings Dialogporten,
    AdapterSettings Adapter,
    AuthenticationSettings Authentication);

public sealed record AuthenticationSettings(string JwtBearerWellKnown);

public record AdapterSettings(Uri BaseUri);

public sealed record DialogportenSettings(Uri BaseUri);

public sealed record AltinnPlatformSettings(Uri BaseUri, Uri InternalStorageEndpoint, Uri InternalRegisterEndpoint, string SubscriptionKey)
{
    public Uri GetAppUriForOrg(string org, string appId) => new($"{BaseUri.Scheme}://{org}.apps.{BaseUri.Host}/{appId}");
    public Uri GetPlatformUri() => new($"{BaseUri.Scheme}://platform.{BaseUri.Host}");
}

public record KeyVaultSettings(string ClientId, string ClientSecret, string TenantId, string SecretUri);

internal sealed record LocalDevelopmentSettings(bool MockDialogportenApi, bool DisableAuth)
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
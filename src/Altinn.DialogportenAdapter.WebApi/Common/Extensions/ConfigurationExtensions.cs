using System.Diagnostics.CodeAnalysis;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Microsoft.Extensions.FileProviders;

namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

internal static class ConfigurationExtensions
{
    public static IConfigurationManager AddCoreClusterSettings(this IConfigurationManager config)
    {
        const string settingsVolume = "/altinn-appsettings";
        const string jsonFileName = "altinn-dbsettings-secret.json";
        IFileProvider fileProvider = Directory.Exists(settingsVolume)
            ? new PhysicalFileProvider(settingsVolume)
            : new NullFileProvider();
        config.AddJsonFile(fileProvider, jsonFileName, optional: true, reloadOnChange: true);
        return config;
    }
    
    public static IConfigurationManager AddAzureKeyVault(this IConfigurationManager config)
    {
        var kvSettings = config
            .GetSection("kvSetting")
            .Get<KeyVaultSettings>();
        
        if (kvSettings is null ||
            string.IsNullOrWhiteSpace(kvSettings.ClientId) ||
            string.IsNullOrWhiteSpace(kvSettings.TenantId) ||
            string.IsNullOrWhiteSpace(kvSettings.ClientSecret) ||
            string.IsNullOrWhiteSpace(kvSettings.SecretUri))
        {
            return config;
        }
    
        var azureCredentials = new ClientSecretCredential(
            tenantId: kvSettings.TenantId,
            clientId: kvSettings.ClientId,
            clientSecret: kvSettings.ClientSecret);

        config.AddAzureKeyVault(new Uri(kvSettings.SecretUri), azureCredentials, 
            new AzureKeyVaultConfigurationOptions{ ReloadInterval = TimeSpan.FromMinutes(5) });
        
        return config;
    }

    public static bool TryGetApplicationInsightsConnectionString(this IConfiguration config, [NotNullWhen(true)] out string? applicationInsightsConnectionString)
    {
        const string vaultApplicationInsightsKey = "ApplicationInsights:InstrumentationKey";
        var foo = config[vaultApplicationInsightsKey];
        applicationInsightsConnectionString = foo is not null 
            ? $"InstrumentationKey={foo}"
            : null;
        return applicationInsightsConnectionString is not null;
    }
}
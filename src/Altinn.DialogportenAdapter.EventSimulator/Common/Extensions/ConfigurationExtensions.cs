using Azure.Identity;
using Microsoft.Extensions.FileProviders;

namespace Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;

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

        config.AddAzureKeyVault(new Uri(kvSettings.SecretUri), azureCredentials);
        return config;
    }
}
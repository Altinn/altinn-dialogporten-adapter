using Altinn.ApiClients.Maskinporten.Config;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

namespace Altinn.DialogportenAdapter.EventSimulator;

public sealed record Settings(DialogportenAdapterSettings DialogportenAdapter);

public sealed record DialogportenAdapterSettings(
    MaskinportenSettings Maskinporten,
    AltinnPlatformSettings Altinn,
    AdapterSettings Adapter,
    AzureStorageSettings AzureStorage);

public record AzureStorageSettings(string ConnectionString)
{
    public static string GetTableName(IHostEnvironment hostEnvironment) =>
        $"{hostEnvironment.EnvironmentName}{nameof(MigrationPartitionEntity)}";
}

public record AdapterSettings(Uri InternalBaseUri);

public sealed record AltinnPlatformSettings(Uri ApiStorageEndpoint);

public record KeyVaultSettings(string ClientId, string ClientSecret, string TenantId, string SecretUri);
using Altinn.ApiClients.Maskinporten.Config;

namespace Altinn.DialogportenAdapter.EventSimulator;

public sealed record Settings(DialogportenAdapterSettings DialogportenAdapter);

public sealed record DialogportenAdapterSettings(
    MaskinportenSettings Maskinporten, 
    AltinnPlatformSettings Altinn, 
    AdapterSettings Adapter);

public record AdapterSettings(Uri InternalBaseUri);

public sealed record AltinnPlatformSettings(Uri ApiStorageEndpoint);

public record KeyVaultSettings(string ClientId, string ClientSecret, string TenantId, string SecretUri);
using Altinn.ApiClients.Maskinporten.Config;

namespace Altinn.DialogportenAdapter.WebApi;

public sealed record Settings(DialogportenAdapterSettings DialogportenAdapter);

public sealed record DialogportenAdapterSettings(MaskinportenSettings Maskinporten, AltinnPlatformSettings Altinn, DialogportenSettings Dialogporten, AdapterSettings Adapter);

public record AdapterSettings(Uri BaseUri);

public sealed record DialogportenSettings(Uri BaseUri);

public sealed record AltinnPlatformSettings(Uri BaseUri, Uri ApiStorageEndpoint, string SubscriptionKey)
{
    public Uri GetAppUriForOrg(string org) => new($"{BaseUri.Scheme}://{org}.apps.{BaseUri.Host}");
    public Uri GetPlatformUri() => new($"{BaseUri.Scheme}://platform.{BaseUri.Host}");
}
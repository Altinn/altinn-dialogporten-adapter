using Altinn.ApiClients.Maskinporten.Config;

namespace Altinn.Platform.DialogportenAdapter.WebApi;

public sealed record Settings(InfrastructureSettings Infrastructure);

public sealed record InfrastructureSettings(MaskinportenSettings Maskinporten, AltinnPlatformSettings Altinn, DialogportenSettings Dialogporten);

public sealed record DialogportenSettings(Uri BaseUri);

public sealed record AltinnPlatformSettings(Uri BaseUri, string SubscriptionKey)
{
    public Uri PlatformBaseUri => new Uri($"{BaseUri.Scheme}://platform.{BaseUri.Host}");
    public Uri GetAppUriForOrg(string org) => new Uri($"{BaseUri.Scheme}://{org}.apps.{BaseUri.Host}");
}
using Altinn.ApiClients.Maskinporten.Config;

namespace Altinn.Platform.DialogportenAdapter.WebApi;

public sealed record Settings(InfrastructureSettings Infrastructure);

public sealed record InfrastructureSettings(MaskinportenSettings Maskinporten, AltinnPlatformSettings Altinn, DialogportenSettings Dialogporten);

public sealed record DialogportenSettings(Uri BaseUri);

public sealed record AltinnPlatformSettings(Uri BaseUri, string SubscriptionKey);
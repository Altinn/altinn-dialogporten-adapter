using Altinn.DialogportenAdapter.WebApi.Common;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;


namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;

public record Org(
    [property: JsonPropertyName("name")] Dictionary<string, string> Name,
    [property: JsonPropertyName("orgnr")] string OrgNr,
    [property: JsonPropertyName("environments")] List<string> Environments,
    [property: JsonPropertyName("logo")] string? Logo,
    [property: JsonPropertyName("emblem")] string? Emblem,
    [property: JsonPropertyName("homepage")] string? HomePage,
    [property: JsonPropertyName("contact")] OrgContact? Contact = null);

public record OrgContact(
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("url")] string? Url);

public record AltinnOrgData(
    [property: JsonPropertyName("orgs")] Dictionary<string, Org> Orgs);

internal interface IAltinnOrgs
{
    Task<AltinnOrgData?> GetAltinnOrgs(CancellationToken cancellationToken);
}

internal sealed class AltinnOrgs(IFusionCache cache, IHttpClientFactory clientFactory, IOptionsSnapshot<Settings> settings) : IAltinnOrgs
{
    private readonly IHttpClientFactory _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    private readonly IFusionCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly Settings _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));

    public Task<AltinnOrgData?> GetAltinnOrgs(CancellationToken cancellationToken) =>
        _cache.GetOrSetAsync(
            key: nameof(AltinnOrgData),
            factory: FetchAltinnOrgData,
            token: cancellationToken).AsTask();

    private async Task<AltinnOrgData?> FetchAltinnOrgData(CancellationToken ct) =>
        await _clientFactory.CreateClient(Constants.AltinnOrgsClient)
            .GetFromJsonAsync<AltinnOrgData>(_settings.DialogportenAdapter.Altinn.AltinnOrgs, ct);
}

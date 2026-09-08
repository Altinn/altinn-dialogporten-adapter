using System.Text.Json.Serialization;
using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Common.Exceptions;
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
    Task<AltinnOrgData> GetAltinnOrgs(CancellationToken cancellationToken);
    Task<AltinnOrgData?> TryGetAltinnOrgs(CancellationToken cancellationToken);
}

internal sealed partial class AltinnOrgs(
    IFusionCache cache,
    IHttpClientFactory clientFactory,
    IOptionsSnapshot<Settings> settings,
    ILogger<AltinnOrgs> logger) : IAltinnOrgs
{
    private readonly IHttpClientFactory _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    private readonly IFusionCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly Settings _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));

    [LoggerMessage(LogLevel.Warning, "Error occured: {errorMessage}")]
    private partial void LogAltinnOrgsWarning(string errorMessage);

    public async Task<AltinnOrgData> GetAltinnOrgs(CancellationToken cancellationToken)
    {
        return await _cache
            .GetOrSetAsync(key: nameof(AltinnOrgData), factory: FetchAltinnOrgData, token: cancellationToken)
            .AsTask();
    }

    public async Task<AltinnOrgData?> TryGetAltinnOrgs(CancellationToken cancellationToken)
    {
        try
        {
            return await GetAltinnOrgs(cancellationToken);
        }
        catch (Exception)
        {
            LogAltinnOrgsWarning("Failed to get AltinnOrgs from server. Using cached");
            return null;
        }
    }

    private async Task<AltinnOrgData> FetchAltinnOrgData(CancellationToken ct)
    {
        var data = await _clientFactory
                       .CreateClient(Constants.AltinnOrgsClient)
                       .GetFromJsonAsync<AltinnOrgData>(_settings.DialogportenAdapter.Altinn.AltinnOrgs, ct)
                   ?? throw new AltinnOrgsApiUnavailableException("Altinn orgs serialized to null");

        return data.Orgs is not null
            ? data
            : throw new AltinnOrgsApiUnavailableException("Altinn orgs serialized without any organizations");
    }
}

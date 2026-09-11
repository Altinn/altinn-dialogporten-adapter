using System.Diagnostics;
using System.Text.Json.Serialization;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.AltinnCdn;

public record Org(
    [property: JsonPropertyName("name")] Dictionary<string, string> Name,
    [property: JsonPropertyName("orgnr")] string OrgNr);

public record AltinnOrgData(
    [property: JsonPropertyName("orgs")] Dictionary<string, Org> Orgs);

internal interface IAltinnCdnRepository
{
    Task<AltinnOrgData> GetAltinnOrgs(CancellationToken cancellationToken);
}

internal sealed class AltinnCdnRepository(IAltinnCdnApi altinnCdnApi, IFusionCache cache) : IAltinnCdnRepository
{
    private readonly IFusionCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));


    public async Task<AltinnOrgData> GetAltinnOrgs(CancellationToken cancellationToken)
    {
        return await _cache
            .GetOrSetAsync(key: nameof(AltinnOrgData), factory: FetchAltinnOrgData, token: cancellationToken)
            .AsTask();
    }

    private async Task<AltinnOrgData> FetchAltinnOrgData(CancellationToken ct)
    {
        var response = await altinnCdnApi.GetAltinnOrgs(ct).EnsureSuccess();
        var content = response.Content ?? throw new UnreachableException("AltinnOrgData serialized to null");
        if (content.Orgs == null) throw new UnreachableException("AltinnOrgData.Orgs serialized to null");

        return content;
    }
}

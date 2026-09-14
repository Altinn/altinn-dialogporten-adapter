using System.Diagnostics;
using ZiggyCreatures.Caching.Fusion;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.AltinnCdn;

internal interface IAltinnCdnRepository
{
    Task<AltinnOrgData> GetAltinnOrgs(CancellationToken cancellationToken);
}

internal sealed class AltinnCdnRepository(IAltinnCdnApi altinnCdnApi, IFusionCache cache) : IAltinnCdnRepository
{
    private readonly IAltinnCdnApi _altinnCdnApi = altinnCdnApi ?? throw new ArgumentNullException(nameof(altinnCdnApi));
    private readonly IFusionCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));


    public async Task<AltinnOrgData> GetAltinnOrgs(CancellationToken cancellationToken)
    {
        return await _cache
            .GetOrSetAsync(key: nameof(AltinnOrgData), factory: FetchAltinnOrgData, token: cancellationToken)
            .AsTask();
    }

    private async Task<AltinnOrgData> FetchAltinnOrgData(CancellationToken ct)
    {
        var content = await _altinnCdnApi.GetAltinnOrgs(ct);
        if (content.Orgs == null) throw new UnreachableException("AltinnOrgData.Orgs serialized to null");

        return content;
    }
}

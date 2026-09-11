using Refit;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.AltinnCdn;

public interface IAltinnCdnApi
{
    [Get("/orgs/altinn-orgs.json")]
    Task<IApiResponse<AltinnOrgData>> GetAltinnOrgs(CancellationToken cancellationToken);
}

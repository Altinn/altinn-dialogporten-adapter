using Refit;

namespace Altinn.Platform.DialogportenAdapter.EventSimulator.Infrastructure;

internal interface IAltinnCdnApi
{
    [Get("/orgs/altinn-orgs.json")]
    Task<OrganizationRegistryResponse> GetOrgs(CancellationToken cancellationToken);
}

internal sealed class OrganizationRegistryResponse
{
    public required IDictionary<string, OrganizationDetails> Orgs { get; init; }
}

internal sealed class OrganizationDetails
{
    public required string Orgnr { get; init; }
}
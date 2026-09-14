using System.Text.Json.Serialization;
using Refit;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.AltinnCdn;

public interface IAltinnCdnApi
{
    [Get("/orgs/altinn-orgs.json")]
    Task<AltinnOrgData> GetAltinnOrgs(CancellationToken cancellationToken);
}

public record Org(
    [property: JsonPropertyName("name")] Dictionary<string, string> Name,
    [property: JsonPropertyName("orgnr")] string OrgNr);

public record AltinnOrgData(
    [property: JsonPropertyName("orgs")] Dictionary<string, Org> Orgs);

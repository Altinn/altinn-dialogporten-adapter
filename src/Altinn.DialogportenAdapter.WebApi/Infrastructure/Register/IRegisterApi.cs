using Refit;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;

internal interface IRegisterApi
{
    [Post("/register/api/v1/dialogporten/parties/query")]
    Task<PartyQueryResponse> GetPartiesByUrns(PartyQueryRequest request, CancellationToken cancellationToken);
}

internal sealed record PartyQueryResponse(
    List<PartyIdentifier> Data
);

internal sealed record PartyIdentifier(
    int? PartyId,
    string PartyType,
    string DisplayName,
    string? PersonIdentifier,
    string? OrganizationIdentifier
);

internal sealed record PartyQueryRequest(List<string> Data);
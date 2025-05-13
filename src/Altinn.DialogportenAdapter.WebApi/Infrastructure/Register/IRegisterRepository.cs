using System.Diagnostics;
using Altinn.DialogportenAdapter.WebApi.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;

internal interface IRegisterRepository
{
    Task<Dictionary<string, PartyIdentifier>> GetByUrn(IEnumerable<string> urns, CancellationToken cancellationToken);
    Task<string?> GetUserUrn(string userId, CancellationToken cancellationToken);
    Task<string?> GetPartyUrn(string partyId, CancellationToken cancellationToken);
}

internal sealed class RegisterRepository : IRegisterRepository
{
    private readonly IRegisterApi _registerApi;
    private readonly IFusionCache _cache;

    public RegisterRepository(IRegisterApi registerApi, IFusionCache cache)
    {
        _registerApi = registerApi;
        _cache = cache;
    }

    public Task<string?> GetPartyUrn(string partyId, CancellationToken cancellationToken) =>
        GetUrn(Constants.PartyIdUrnPrefix + partyId, cancellationToken);

    public Task<string?> GetUserUrn(string userId, CancellationToken cancellationToken) =>
        GetUrn(Constants.UserIdUrnPrefix + userId, cancellationToken);

    public async Task<Dictionary<string, PartyIdentifier>> GetByUrn(IEnumerable<string> urns, CancellationToken cancellationToken)
    {
        var fetchTasks = urns
            .Distinct()
            .Select(urn => _cache
                .GetOrSetAsync(
                    key: urn,
                    factory: ct => FetchUrn(urn, ct),
                    token: cancellationToken)
                .AsTask());
        var results = await Task.WhenAll(fetchTasks);
        return results
            .Where(x => x.Identifier is not null)
            .ToDictionary(x => x.Urn, x => x.Identifier!);
    }

    private async Task<string?> GetUrn(string id, CancellationToken cancellationToken)
    {
        var results = await GetByUrn([id], cancellationToken);
        if (!results.TryGetValue(id, out var result))
        {
            return null;
        }

        Debug.Assert(result.OrganizationIdentifier is not null != result.PersonIdentifier is not null);

        if (result.OrganizationIdentifier is not null)
        {
            return Constants.OrganizationUrnPrefix + result.OrganizationIdentifier;
        }

        if (result.PersonIdentifier is not null)
        {
            return Constants.PersonUrnPrefix + result.PersonIdentifier;
        }

        throw new UnreachableException("Unknown party id.");
    }

    private async Task<(string Urn, PartyIdentifier? Identifier)> FetchUrn(string urn,
        CancellationToken cancellationToken)
    {
        // TODO: Remove this if when register supports user urn
        if (urn.StartsWith(Constants.UserIdUrnPrefix))
        {
            return (urn, null);
        }

        var results = await _registerApi.GetPartiesByUrns(new PartyQueryRequest([urn]), cancellationToken);
        var result = results.Data.FirstOrDefault();
        return (urn, result);
    }
}

using System.Diagnostics;
using Altinn.DialogportenAdapter.WebApi.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;

internal interface IRegisterRepository
{
    Task<Dictionary<string, string>> GetActorUrnByUserId(IEnumerable<string> userIds, CancellationToken cancellationToken);
    Task<Dictionary<string, string>> GetActorUrnByPartyId(IEnumerable<string> partyIds, CancellationToken cancellationToken);
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

    public async Task<Dictionary<string, string>> GetActorUrnByUserId(IEnumerable<string> userIds,
        CancellationToken cancellationToken)
    {
        var results = await FetchUrns(
            userIds.Select(x => Constants.UserIdUrnPrefix + x),
            cancellationToken);
        return results
            .Where(x => x.AktorUrn is not null)
            .ToDictionary(x => x.RegisterUrn[Constants.UserIdUrnPrefix.Length..], x => x.AktorUrn!);
    }

    public async Task<Dictionary<string, string>> GetActorUrnByPartyId(IEnumerable<string> partyIds,
        CancellationToken cancellationToken)
    {
        var results = await FetchUrns(
            partyIds.Select(x => Constants.PartyIdUrnPrefix + x),
            cancellationToken);
        return results
            .Where(x => x.AktorUrn is not null)
            .ToDictionary(x => x.RegisterUrn[Constants.PartyIdUrnPrefix.Length..], x => x.AktorUrn!);
    }

    private Task<(string RegisterUrn, string? AktorUrn)[]> FetchUrns(
        IEnumerable<string> registerUrns,
        CancellationToken cancellationToken) =>
        Task.WhenAll(registerUrns
            .Distinct()
            .Select(urn => _cache
                .GetOrSetAsync(
                    key: urn,
                    factory: ct => FetchUrn(urn, ct),
                    token: cancellationToken)
                .AsTask()));

    private async Task<(string RegisterUrn, string? ActorUrn)> FetchUrn(string registerUrn,
        CancellationToken cancellationToken)
    {
        var results = await _registerApi.GetPartiesByUrns(new PartyQueryRequest([registerUrn]), cancellationToken);
        return results.Data.FirstOrDefault() switch
        {
            null => (registerUrn, null),
            // Use externalUrn if presented by Register
            { ExternalUrn: not null and var externalUrn } => (registerUrn, externalUrn),
            { PersonIdentifier: not null and var personId } => (registerUrn, Constants.PersonUrnPrefix + personId),
            { DisplayName: not null and var displayName, PartyType: "self-identified-user" } => (registerUrn, Constants.SiUserUrnPrefix + displayName),
            // The below is to handle legacy enterprise users and system ids
            { DisplayName: not null and var displayName } => (registerUrn, Constants.DisplayNameUrnPrefix + displayName),
            _ => throw new UnreachableException("Invalid response from register.")
        };
    }
}

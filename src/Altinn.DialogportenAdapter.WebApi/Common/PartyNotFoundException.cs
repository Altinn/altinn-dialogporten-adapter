namespace Altinn.DialogportenAdapter.WebApi.Common;

public class PartyNotFoundException(string? partyId) : InvalidOperationException($"Party with id {partyId} not found");
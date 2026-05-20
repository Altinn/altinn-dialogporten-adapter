namespace Altinn.DialogportenAdapter.WebApi.Common.Exceptions;

public class PartyNotFoundException(string? partyId) : InvalidOperationException($"Party with id {partyId} not found");

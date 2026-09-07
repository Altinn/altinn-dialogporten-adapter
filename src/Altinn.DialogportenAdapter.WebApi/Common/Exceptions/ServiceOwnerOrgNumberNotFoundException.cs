namespace Altinn.DialogportenAdapter.WebApi.Common.Exceptions;

public class ServiceOwnerOrgNumberNotFoundException(string? orgCode) : InvalidOperationException($"Organization number for service owner {orgCode} not found");

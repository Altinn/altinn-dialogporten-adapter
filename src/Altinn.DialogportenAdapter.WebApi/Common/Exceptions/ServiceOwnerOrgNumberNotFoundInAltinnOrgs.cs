namespace Altinn.DialogportenAdapter.WebApi.Common.Exceptions;

public class ServiceOwnerOrgNumberNotFoundInAltinnOrgs(string? orgCode) : InvalidOperationException($"Organization number for service owner {orgCode} not found in Altinn Orgs");

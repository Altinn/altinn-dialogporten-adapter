namespace Altinn.DialogportenAdapter.WebApi.Common.Exceptions;

public class ServiceOwnerOrgNumberNotFoundInAltinnOrgs(string reason) : InvalidOperationException(reason);

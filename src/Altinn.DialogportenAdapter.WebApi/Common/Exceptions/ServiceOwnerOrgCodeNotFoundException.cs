namespace Altinn.DialogportenAdapter.WebApi.Common.Exceptions;

/// <summary>
/// This is a fatal exception. We always expect app/instance to have an org set.
/// </summary>
public class ServiceOwnerOrgCodeNotFoundException() : InvalidOperationException("Unable to find org in either application or instance");

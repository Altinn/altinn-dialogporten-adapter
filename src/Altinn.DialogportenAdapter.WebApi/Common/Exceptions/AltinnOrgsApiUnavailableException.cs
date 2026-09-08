namespace Altinn.DialogportenAdapter.WebApi.Common.Exceptions;

public class AltinnOrgsApiUnavailableException(string? message) : InvalidOperationException(message ?? "Altinn orgs could not be fetched");

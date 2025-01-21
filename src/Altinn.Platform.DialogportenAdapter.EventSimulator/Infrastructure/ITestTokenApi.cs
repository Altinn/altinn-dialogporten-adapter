using Refit;

namespace Altinn.Platform.DialogportenAdapter.EventSimulator.Infrastructure;

internal interface ITestTokenApi
{
    [Get("/api/GetEnterpriseToken?env=tt02&ttl=86400&scopes=altinn:serviceowner/instances.read")]
    Task<string> GetToken([Query] string org, [Query] string orgNo, CancellationToken cancellationToken);
}

using Refit;

namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

internal interface IStorageApi
{
    [Get("/storage/api/v1/applications")]
    Task<ApplicationResponse> GetApplications(CancellationToken cancellationToken);
}
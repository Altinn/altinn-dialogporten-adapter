using Refit;

namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Adapter;

public interface IStorageAdapterApi
{
    [Post("/api/v1/syncDialog")]
    Task<IApiResponse> Sync([Body] InstanceEvent syncDialogDto, CancellationToken cancellationToken);
}
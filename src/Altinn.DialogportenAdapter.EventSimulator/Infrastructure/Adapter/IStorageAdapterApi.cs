using Altinn.DialogportenAdapter.Contracts;
using Refit;

namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Adapter;

public interface IStorageAdapterApi
{
    [Post("/storage/dialogporten/api/v1/syncDialog")]
    Task<IApiResponse> Sync([Body] SyncInstanceCommand syncDialogDto, CancellationToken cancellationToken);
}
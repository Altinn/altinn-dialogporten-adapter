using Altinn.Storage.Contracts;
using Refit;

namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

public interface IStorageAdapterApi
{
    [Post("/storage/dialogporten/api/v1/syncDialog")]
    Task<IApiResponse> Sync([Body] InstanceUpdatedEvent syncDialogDto, CancellationToken cancellationToken);
}
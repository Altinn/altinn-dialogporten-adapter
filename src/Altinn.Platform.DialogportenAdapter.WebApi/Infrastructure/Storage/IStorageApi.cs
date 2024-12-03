using Altinn.Platform.Storage.Interface.Models;
using Refit;

namespace Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Storage;

public interface IStorageApi
{
    [Get("/storage/api/v1/applications/{**appId}")]
    Task<Application> GetApplication(string appId, CancellationToken cancellationToken = default);

    [Get("/storage/api/v1/instances/{partyId}/{instanceId}")]
    Task<Instance> GetInstance(int partyId, Guid instanceId, CancellationToken cancellationToken = default);

    [Put("/storage/api/v1/instances/{partyId}/{instanceId}/datavalues")]
    Task UpdateDataValues(int partyId, Guid instanceId, [Body] DataValues dataValues, CancellationToken cancellationToken = default);
    
    [Get("/storage/api/v1/instances/{partyId}/{instanceId}/events")]
    Task<InstanceEventList> GetInstanceEvents(int partyId, Guid instanceId, CancellationToken cancellationToken = default);
}

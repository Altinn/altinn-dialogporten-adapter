using Altinn.Platform.Storage.Interface.Models;
using Refit;

namespace Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Storage;

internal interface IStorageApi
{
    [Get("/storage/api/v1/applications/{**appId}")]
    Task<IApiResponse<Application>> GetApplication(string appId, CancellationToken cancellationToken = default);

    [Get("/storage/api/v1/instances/{partyId}/{instanceId}")]
    Task<IApiResponse<Instance>> GetInstance(int partyId, Guid instanceId, CancellationToken cancellationToken = default);

    [Put("/storage/api/v1/instances/{partyId}/{instanceId}/datavalues")]
    Task UpdateDataValues(int partyId, Guid instanceId, [Body] DataValues dataValues, CancellationToken cancellationToken = default);
    
    [Get("/storage/api/v1/instances/{partyId}/{instanceId}/events")]
    Task<IApiResponse<InstanceEventList>> GetInstanceEvents(int partyId, Guid instanceId, 
        [Query(CollectionFormat.Multi)] IEnumerable<string> eventTypes, 
        CancellationToken cancellationToken = default);
    
    [Delete("/storage/api/v1/sbl/instances/{**instanceId}")]
    Task DeleteInstance(string instanceId, [Query] bool hard = false, CancellationToken cancellationToken = default);
}
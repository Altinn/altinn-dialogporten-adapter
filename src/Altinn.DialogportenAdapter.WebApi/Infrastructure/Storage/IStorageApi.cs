using Altinn.Platform.Storage.Interface.Models;
using Refit;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;

internal interface IStorageApi
{
    [Get("/storage/api/v1/applications/{**appId}")]
    Task<IApiResponse<Application>> GetApplication(string appId, CancellationToken cancellationToken = default);

    [Get("/storage/api/v1/instances/{partyId}/{instanceId}")]
    Task<IApiResponse<Instance>> GetInstance(string partyId, Guid instanceId, CancellationToken cancellationToken = default);

    [Put("/storage/api/v1/instances/{partyId}/{instanceId}/datavalues")]
    Task UpdateDataValues(string partyId, Guid instanceId, [Body] DataValues dataValues, CancellationToken cancellationToken = default);

    [Get("/storage/api/v1/instances/{partyId}/{instanceId}/events")]
    Task<IApiResponse<InstanceEventList>> GetInstanceEvents(string partyId, Guid instanceId,
        [Query(CollectionFormat.Multi)] IEnumerable<string> eventTypes,
        CancellationToken cancellationToken = default);

    [Delete("/storage/api/v1/sbl/instances/{partyId}/{instanceId}")]
    Task DeleteInstance(string partyId, Guid instanceId, [Query] bool hard = false, CancellationToken cancellationToken = default);
}
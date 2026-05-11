using Refit;

namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

public interface IStorageApi
{
    [Get("/storage/api/v1/applications")]
    Task<ApplicationResponse> GetApplications(CancellationToken cancellationToken);

    [Get("/storage/api/v1/instances/{partyId}/{instanceId}")]
    Task<InstanceDto> GetInstance(string partyId, Guid instanceId, CancellationToken cancellationToken);
}

public sealed class ApplicationResponse
{
    public List<Application> Applications { get; set; } = null!;
}

public sealed record Application(string Id, string Org);

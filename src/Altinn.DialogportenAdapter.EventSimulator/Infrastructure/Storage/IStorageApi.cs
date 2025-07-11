using Refit;

namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

internal interface IStorageApi
{
    [Get("/storage/api/v1/applications")]
    Task<ApplicationResponse> GetApplications(CancellationToken cancellationToken);
}

internal sealed class ApplicationResponse
{
    public List<Application> Applications { get; set; } = null!;
}

internal sealed record Application(string Id, string Org);
using Refit;

namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

internal interface IStorageApi
{
    [Get("/storage/api/v1/applications")]
    Task<ApplicationResponse> GetApplications(CancellationToken cancellationToken);
    
    [Get("/storage/api/v1/applications/{org}")]
    Task<ApplicationResponse> GetApplicationsForOrg(string org, CancellationToken cancellationToken);
}

internal sealed class ApplicationResponse
{
    public List<Application> Applications { get; set; } = null!;
}

internal sealed record Application(string Id, string Org);
namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

internal interface IOrganizationRepository
{
    ValueTask<List<string>> GetOrganizations(CancellationToken cancellationToken);
}

internal class OrganizationRepository : IOrganizationRepository
{
    private readonly IStorageApi _storageApi;

    public OrganizationRepository(IStorageApi storageApi)
    {
        _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
    }

    public async ValueTask<List<string>> GetOrganizations(CancellationToken cancellationToken)
    {
        var apps = await _storageApi.GetApplications(cancellationToken);
        return apps.Applications
            .Select(x => x.Org)
            .Distinct()
            .ToList();
    }
}
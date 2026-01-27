namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

internal interface IOrganizationRepository
{
    ValueTask<List<string>> GetOrganizations(CancellationToken cancellationToken);
}

internal sealed class OrganizationRepository : IOrganizationRepository
{
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    private readonly IStorageApi _storageApi;
    private static List<string>? _cachedOrganizations;

    public OrganizationRepository(IStorageApi storageApi)
    {
        _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
    }

    public async ValueTask<List<string>> GetOrganizations(CancellationToken cancellationToken)
    {
        if (_cachedOrganizations is not null)
        {
            return _cachedOrganizations;
        }
        await Semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_cachedOrganizations is not null)
            {
                return _cachedOrganizations;
            }

            var apps = await _storageApi.GetApplications(cancellationToken);
            return _cachedOrganizations = apps.Applications
                .Select(x => x.Org)
                .Distinct()
                .ToList();
        }
        finally
        {
            Semaphore.Release();
        }
    }
}
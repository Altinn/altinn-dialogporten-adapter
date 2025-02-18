using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

namespace Altinn.DialogportenAdapter.EventSimulator;

internal interface IStorageApplicationService
{
    Task<IStorageApplicationContext[]> GetApplicationInfo(CancellationToken cancellationToken);
}

internal interface IStorageApplicationContext
{
    string AppId { get; }
    Task<string> GetToken(CancellationToken cancellationToken);
}

internal sealed class DigdirStorageApplicationService : IStorageApplicationService
{
    private readonly IStorageApi _storageApi;
    private readonly IAltinnCdnApi _altinnCdnApi;
    private readonly ITestTokenApi _testTokenApi;

    public DigdirStorageApplicationService(IStorageApi storageApi, IAltinnCdnApi altinnCdnApi, ITestTokenApi testTokenApi)
    {
        _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
        _altinnCdnApi = altinnCdnApi ?? throw new ArgumentNullException(nameof(altinnCdnApi));
        _testTokenApi = testTokenApi ?? throw new ArgumentNullException(nameof(testTokenApi));
    }

    public async Task<IStorageApplicationContext[]> GetApplicationInfo(CancellationToken cancellationToken)
    {
        var orgsResponseTask = _altinnCdnApi.GetOrgs(cancellationToken);
        var applicationsTask = _storageApi.GetApplicationsForOrg("digdir", cancellationToken);
        await Task.WhenAll(orgsResponseTask, applicationsTask);
        var orgs = orgsResponseTask.Result.Orgs;
        var applications = applicationsTask.Result.Applications;
        
        var appInfos = applications
            .Join(orgs, x => x.Org, x => x.Key,
                (app, org) => (app.Id, app.Org, org.Value.Orgnr),
                StringComparer.OrdinalIgnoreCase)
            .Distinct()
            .ToList();
        
        var orgTokens = await Task.WhenAll(appInfos
            .Select(x => (x.Org, x.Orgnr))
            .Distinct()
            .Select(async x => (x.Org, Token: await _testTokenApi.GetToken(x.Org, x.Orgnr, cancellationToken))));

        var appContexts = appInfos
            .Join(orgTokens, x => x.Org, x => x.Org,
                (app, token) => new AppInfo(app.Id, token.Token),
                StringComparer.OrdinalIgnoreCase)
            .ToArray<IStorageApplicationContext>();
        
        return appContexts;
    }
    
    private record AppInfo(string AppId, string OrgToken) : IStorageApplicationContext
    {
        public Task<string> GetToken(CancellationToken cancellationToken) => Task.FromResult(OrgToken);
    }
}

internal sealed class StorageApplicationService : IStorageApplicationService
{
    private readonly IStorageApi _storageApi;
    private readonly IAltinnCdnApi _altinnCdnApi;
    private readonly ITestTokenApi _testTokenApi;

    public StorageApplicationService(IStorageApi storageApi, IAltinnCdnApi altinnCdnApi, ITestTokenApi testTokenApi)
    {
        _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
        _altinnCdnApi = altinnCdnApi ?? throw new ArgumentNullException(nameof(altinnCdnApi));
        _testTokenApi = testTokenApi ?? throw new ArgumentNullException(nameof(testTokenApi));
    }

    public async Task<IStorageApplicationContext[]> GetApplicationInfo(CancellationToken cancellationToken)
    {
        var orgsResponseTask = _altinnCdnApi.GetOrgs(cancellationToken);
        var applicationsTask = _storageApi.GetApplications( cancellationToken);
        await Task.WhenAll(orgsResponseTask, applicationsTask);
        var orgs = orgsResponseTask.Result.Orgs;
        var applications = applicationsTask.Result.Applications;
        
        var appInfos = applications
            .Join(orgs, x => x.Org, x => x.Key,
                (app, org) => (app.Id, app.Org, org.Value.Orgnr),
                StringComparer.OrdinalIgnoreCase)
            .Distinct()
            .ToList();
        
        var orgTokens = await Task.WhenAll(appInfos
            .Select(x => (x.Org, x.Orgnr))
            .Distinct()
            .Select(async x => (x.Org, Token: await _testTokenApi.GetToken(x.Org, x.Orgnr, cancellationToken))));

        var appContexts = appInfos
            .Join(orgTokens, x => x.Org, x => x.Org,
                (app, token) => new AppInfo(app.Id, token.Token),
                StringComparer.OrdinalIgnoreCase)
            .ToArray<IStorageApplicationContext>();
        
        return appContexts;
    }
    
    private record AppInfo(string AppId, string OrgToken) : IStorageApplicationContext
    {
        public Task<string> GetToken(CancellationToken cancellationToken) => Task.FromResult(OrgToken);
    }
}



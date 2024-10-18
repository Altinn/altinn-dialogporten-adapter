using DialogportenAdapter.Clients;
using DialogportenAdapter.Models;

namespace DialogportenAdapter.Services;

public class InstanceSyncService : IInstanceSyncService
{
    private readonly IDialogportenClient _dialogportenClient;
    private readonly IAppStorageClient _appStorageClient;
    private readonly ILogger<InstanceSyncService> _logger;

    public InstanceSyncService(IAppStorageClient appStorageClient, IDialogportenClient dialogportenClient, ILogger<InstanceSyncService> logger)
    {
        _dialogportenClient = dialogportenClient;
        _appStorageClient = appStorageClient;
        _logger = logger;
    }

    public Task<SynchronizationResult> SyncInstanceAsync(SynchronizationRequest request)
    {
        throw new NotImplementedException();
    }
}

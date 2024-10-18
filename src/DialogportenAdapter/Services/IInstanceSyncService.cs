using DialogportenAdapter.Models;

namespace DialogportenAdapter.Services;

public interface IInstanceSyncService
{
    public Task<SynchronizationResult> SyncInstanceAsync(SynchronizationRequest request);
}

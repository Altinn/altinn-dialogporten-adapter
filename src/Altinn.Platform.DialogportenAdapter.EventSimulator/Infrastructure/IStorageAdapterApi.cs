using Refit;

namespace Altinn.Platform.DialogportenAdapter.EventSimulator.Infrastructure;

public interface IStorageAdapterApi
{
    [Post("/api/v1/syncDialog")]
    Task<IApiResponse> Sync([Body] InstanceEvent syncDialogDto, CancellationToken cancellationToken);
}

public record InstanceEvent(
    string AppId,
    int PartyId, 
    Guid InstanceId,
    DateTimeOffset InstanceCreatedAt,
    bool IsMigration = true);
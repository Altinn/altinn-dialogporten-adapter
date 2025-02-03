using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Delete;

public record DeleteDialogDto(string InstanceId, bool Hard);

internal sealed class DeleteDialogService
{
    private readonly IStorageApi _storageApi;

    public DeleteDialogService(IStorageApi storageApi)
    {
        _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
    }

    public async Task DeleteDialog(DeleteDialogDto request, CancellationToken cancellationToken)
    {
        // TODO: Verifiser dialog token
        await _storageApi.DeleteInstance(request.InstanceId, request.Hard, cancellationToken);
    }
}

// 1: Slett via gui action i AF
// 2: Slett i Dialogporten adapter som verifiserer dialog token
// 3: Kall /storage/api/v1/sbl/instances/:instanceOwnerPartyId/:instanceGuid?hard=<boolean>
// 4: Storage sender slett event til dialogporten adapter (instanceId og CreatedAt)
// 5: Dialogporten adapter sletter dialogen via dialogport api
// 6: Dialogporten vil via GQL subscription si i fra til nettleser at dialogen er slettet
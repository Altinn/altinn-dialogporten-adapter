using Altinn.ApiClients.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Delete;

internal sealed record DeleteInstanceDto(int PartyId, Guid InstanceGuid, string DialogToken);

internal enum DeleteInstanceResult
{
    Success,
    InstanceNotFound,
    Unauthorized
}

internal sealed class InstanceService
{
    private readonly IStorageApi _storageApi;
    private readonly IDialogTokenValidator _dialogTokenValidator;

    public InstanceService(IStorageApi storageApi, IDialogTokenValidator dialogTokenValidator)
    {
        _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
        _dialogTokenValidator = dialogTokenValidator ?? throw new ArgumentNullException(nameof(dialogTokenValidator));
    }

    public async Task<DeleteInstanceResult> Delete(DeleteInstanceDto request, CancellationToken cancellationToken)
    {
        var instance = await _storageApi
            .GetInstance(request.PartyId, request.InstanceGuid, cancellationToken)
            .ContentOrDefault();

        if (instance is null)
        {
            return DeleteInstanceResult.InstanceNotFound;
        }

        var dialogId = request.InstanceGuid.ToVersion7(instance.Created!.Value);
        if (!ValidateDialogToken(request.DialogToken, dialogId))
        {
            return DeleteInstanceResult.Unauthorized;
        }

        // TODO: Skal vi utlede hard delete i noen tilfeller? Basert på status = draft?
        await _storageApi.DeleteInstance(request.PartyId, request.InstanceGuid, hard: false, cancellationToken);
        return DeleteInstanceResult.Success;
    }

    private bool ValidateDialogToken(ReadOnlySpan<char> token, Guid dialogId)
    {
        const string bearerPrefix = "Bearer ";
        token = token.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? token[bearerPrefix.Length..]
            : token;
        var result = _dialogTokenValidator.Validate(token, dialogId, ["delete"]);
        return result.IsValid;
    }
}

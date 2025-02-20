using Altinn.ApiClients.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Delete;

public record DeleteDialogDto(int PartyId, Guid InstanceGuid, bool Hard, string DialogToken);

public enum DeleteDialogResult
{
    Success,
    InstanceNotFound,
    Unauthorized
}

internal sealed class DeleteDialogService
{
    private readonly IStorageApi _storageApi;
    private readonly IDialogTokenValidator _dialogTokenValidator;

    public DeleteDialogService(IStorageApi storageApi, IDialogTokenValidator dialogTokenValidator)
    {
        _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
        _dialogTokenValidator = dialogTokenValidator ?? throw new ArgumentNullException(nameof(dialogTokenValidator));
    }

    public async Task<DeleteDialogResult> DeleteDialog(DeleteDialogDto request, CancellationToken cancellationToken)
    {
        var instance = await _storageApi
            .GetInstance(request.PartyId, request.InstanceGuid, cancellationToken)
            .ContentOrDefault();

        if (instance is null)
        {
            return DeleteDialogResult.InstanceNotFound;
        }

        var dialogId = request.InstanceGuid.ToVersion7(instance.Created!.Value);
        var result = ValidateDialogToken(request.DialogToken, dialogId);
        if (!result.IsValid)
        {
            return DeleteDialogResult.Unauthorized;
        }
        
        await _storageApi.DeleteInstance(request.PartyId, request.InstanceGuid, request.Hard, cancellationToken);
        return DeleteDialogResult.Success;
    }
    
    private IValidationResult ValidateDialogToken(ReadOnlySpan<char> token, Guid dialogId)
    {
        const string bearerPrefix = "Bearer ";
        token = token.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase) 
            ? token[bearerPrefix.Length..] 
            : token;
        var result = _dialogTokenValidator.Validate(token);
        // TODO: Validate dialog id
        // TODO: Validate action
        return result;
    }
}

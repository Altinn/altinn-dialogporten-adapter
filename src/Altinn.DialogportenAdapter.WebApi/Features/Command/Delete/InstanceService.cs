using Altinn.ApiClients.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Delete;

internal sealed record DeleteInstanceDto(string PartyId, Guid InstanceGuid, string DialogToken);

public abstract record DeleteResponse
{
    public sealed record Success : DeleteResponse;

    public sealed record NotFound : DeleteResponse;

    public sealed record UnAuthorized : DeleteResponse;

    public sealed record NotDeletableYet(DateTimeOffset ArchivedAt, int GracePeriod, DateTimeOffset DeletionAllowedAt) : DeleteResponse;
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

    public async Task<DeleteResponse> Delete(DeleteInstanceDto request, CancellationToken cancellationToken)
    {
        var instance = await _storageApi
            .GetInstance(request.PartyId, request.InstanceGuid, cancellationToken)
            .ContentOrDefault();

        if (instance is null)
        {
            return new DeleteResponse.NotFound();
        }

        var dialogId = request.InstanceGuid.ToVersion7(instance.Created!.Value);
        if (!ValidateDialogToken(request.DialogToken, dialogId))
        {
            return new DeleteResponse.UnAuthorized();
        }

        if (instance.Status.Archived.HasValue)
        {
            var app = await _storageApi.GetApplication(instance.AppId, cancellationToken).ContentOrDefault();
            if (app != null && !IsDeletable(instance, app))
            {
                return new DeleteResponse.NotDeletableYet(
                    instance.Status.Archived.Value,
                    app.PreventInstanceDeletionForDays!.Value,
                    new DateTimeOffset(instance.Status.Archived.Value.ToUniversalTime().Date.AddDays(app.PreventInstanceDeletionForDays.Value), TimeSpan.Zero));
            }
        }

        // TODO: Skal vi utlede hard delete i noen tilfeller? Basert på status = draft?
        await _storageApi.DeleteInstance(request.PartyId, request.InstanceGuid, hard: false, cancellationToken);
        return new DeleteResponse.Success();
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

    private static bool IsDeletable(Instance instance, Application app)
    {
        if (!instance.Status.Archived.HasValue)
        {
            return false;
        }

        if (app.PreventInstanceDeletionForDays == null)
        {
            return true;
        }

        var instanceDate = instance.Status.Archived.Value.ToUniversalTime().Date;
        var deletableAt = instanceDate.AddDays(app.PreventInstanceDeletionForDays.Value);
        return deletableAt <= DateTime.UtcNow.Date;
    }
}

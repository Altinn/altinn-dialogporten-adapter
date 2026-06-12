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
    private readonly IApplicationsApi _applicationsApi;
    private readonly IDialogTokenValidator _dialogTokenValidator;

    public InstanceService(IStorageApi storageApi, IDialogTokenValidator dialogTokenValidator, IApplicationsApi applicationsApi)
    {
        _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
        _dialogTokenValidator = dialogTokenValidator ?? throw new ArgumentNullException(nameof(dialogTokenValidator));
        _applicationsApi = applicationsApi ?? throw new ArgumentNullException(nameof(applicationsApi));
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

        if (!ValidateDialogToken(request.DialogToken, dialogId, instance))
        {
            return new DeleteResponse.UnAuthorized();
        }

        if (instance.Status.Archived.HasValue)
        {
            var app = await _applicationsApi.GetApplication(instance.AppId, cancellationToken).ContentOrDefault();
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

    private bool ValidateDialogToken(ReadOnlySpan<char> token, Guid dialogId, Instance instance)
    {
        const string bearerPrefix = "Bearer ";
        token = token.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? token[bearerPrefix.Length..]
            : token;
        var result = _dialogTokenValidator.Validate(token, dialogId, ["delete"]);

        if (!result.IsValid)
        {
            // The dialog token might contain an action including the current task, ie "delete,urn:altinn:task:Task_1"
            // We need to check that as well
            var currentTaskId = GetCurrentProcessTask(instance);
            if (currentTaskId != null)
            {
                result = _dialogTokenValidator.Validate(token, dialogId, [$"delete,{currentTaskId}"]);
            }
        }
        return result.IsValid;
    }

    private static string? GetCurrentProcessTask(Instance instance)
    {
        return !string.IsNullOrWhiteSpace(instance.Process?.CurrentTask?.ElementId)
            ? "urn:altinn:task:" + instance.Process.CurrentTask.ElementId
            : null;
    }

    private static bool IsDeletable(Instance instance, Application app)
    {
        if (!instance.Status.Archived.HasValue)
        {
            return true;
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

using Altinn.Storage.Contracts;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

public static class SyncDialogOnInstanceUpdatedHandler
{
    public static Task Handle(InstanceUpdatedEvent message,
        ISyncInstanceToDialogService syncService,
        CancellationToken cancellationToken) =>
        syncService.Sync(message, cancellationToken);
}
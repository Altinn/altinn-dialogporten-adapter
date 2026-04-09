using Altinn.DialogportenAdapter.Contracts;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

public class SyncDialogOnInstanceUpdatedHandler
{
    public static Task Handle(SyncInstanceCommand message,
        ISyncInstanceToDialogService syncService,
        CancellationToken cancellationToken) =>
        syncService.Sync(message, cancellationToken);
}

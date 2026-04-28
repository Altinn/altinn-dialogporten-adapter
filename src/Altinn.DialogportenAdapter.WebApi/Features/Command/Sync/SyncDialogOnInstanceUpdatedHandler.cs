using Altinn.DialogportenAdapter.Contracts;
using Wolverine;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

public static class SyncDialogOnInstanceUpdatedHandler
{
    public static Task Handle(SyncInstanceCommand message,
        ISyncInstanceToDialogService syncService,
        IMessageContext context,
        CancellationToken cancellationToken) =>
        syncService.Sync(message, (context.Envelope?.Attempts  ?? 0) + 1, cancellationToken);
}

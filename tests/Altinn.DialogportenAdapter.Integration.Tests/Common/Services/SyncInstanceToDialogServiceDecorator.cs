using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

namespace Altinn.DialogportenAdapter.Integration.Tests.Common.Services;

internal sealed class SyncInstanceToDialogServiceDecorator(
    SyncInstanceToDialogService inner,
    SyncCompletionSignal signal) : ISyncInstanceToDialogService
{
    public async Task Sync(SyncInstanceCommand dto, CancellationToken cancellationToken = default)
    {
        await inner.Sync(dto, cancellationToken);
        signal.Completed.TrySetResult();
    }
}

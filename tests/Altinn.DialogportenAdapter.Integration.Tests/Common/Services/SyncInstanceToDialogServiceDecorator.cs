using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

namespace Altinn.DialogportenAdapter.Integration.Tests.Common.Services;

internal sealed class SyncInstanceToDialogServiceDecorator(
    SyncInstanceToDialogService inner,
    SyncCompletionSignal signal) : ISyncInstanceToDialogService
{
    public async Task Sync(SyncInstanceCommand dto, int currentAttempt = 1, CancellationToken cancellationToken = default)
    {
        await inner.Sync(dto, currentAttempt, cancellationToken);
        signal.Completed.TrySetResult();
    }
}

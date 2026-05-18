namespace Altinn.DialogportenAdapter.Integration.Tests.Common.Services;

internal sealed class SyncCompletionSignal
{
    public TaskCompletionSource Completed { get; private set; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Reset() => Completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

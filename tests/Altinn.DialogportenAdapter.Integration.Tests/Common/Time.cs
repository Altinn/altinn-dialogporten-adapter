using Xunit;

namespace Altinn.DialogportenAdapter.Integration.Tests.Common;

public static class Time
{
    public static async Task<T?> WaitUntilAsync<T>(
        Func<T?> condition,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null
    )
    {
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < (timeout ?? TimeSpan.FromSeconds(5)))
        {
            var result = condition();
            if (result != null) return result;

            await Task.Delay(pollInterval ?? TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        }

        return default;
    }
}

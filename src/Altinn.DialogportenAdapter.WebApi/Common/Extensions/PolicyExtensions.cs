using Wolverine.ErrorHandling;

namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

public static class PolicyExtensions
{
    private static readonly Random Random = new();

    public static IAdditionalActions RetryWithJitteredCooldown(this PolicyExpression policyExpression, params TimeSpan[] delays)
    {
        // Jitter +/- 50% on the supplied delays
        for (var i = 0; i < delays.Length; i++)
        {
            delays[i] += TimeSpan.FromMilliseconds(Random.Next(
                (int)(-delays[i].TotalMilliseconds / 2),
                (int)(delays[i].TotalMilliseconds / 2)));
        }

        return policyExpression.RetryWithCooldown(delays);
    }
}
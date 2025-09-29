using Wolverine.ErrorHandling;

namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

public static class PolicyExtensions
{
    public static IAdditionalActions RetryWithJitteredCooldown(this PolicyExpression policyExpression, params TimeSpan[] delays)
    {
        var jittered = new TimeSpan[delays.Length];

        for (var i = 0; i < delays.Length; i++)
        {
            var delay = delays[i];
            var factor = 0.5 + Random.Shared.NextDouble(); // +/- 50 %
            jittered[i] = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * factor);
        }

        return policyExpression.RetryWithCooldown(jittered);
    }
}
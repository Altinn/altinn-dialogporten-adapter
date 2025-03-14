using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Altinn.DialogportenAdapter.EventSimulator.Common;

internal sealed class HealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            HealthCheckResult.Healthy("A healthy result."));
    }
}

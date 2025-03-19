using System.Diagnostics;
using OpenTelemetry;

namespace Altinn.DialogportenAdapter.WebApi.Common.Health;

internal sealed class HealthCheckFilterProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        var requestPath = activity.Tags.FirstOrDefault(t => t.Key == "http.route").Value;
        if (requestPath?.EndsWith("/health") ?? false)
        {
            // Drop this telemetry
            activity.IsAllDataRequested = false;
            return; 
        }

        base.OnEnd(activity);
    }
}
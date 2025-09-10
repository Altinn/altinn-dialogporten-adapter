using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.AzureServiceBus;

namespace Altinn.DialogportenAdapter.Contracts;

public static class WolverineOptionsExtentions
{
    public static WolverineOptions ConfigureAdapterDefaults(
        this WolverineOptions opts,
        IHostEnvironment env,
        string azureServiceBusConnectionString)
    {
        ArgumentNullException.ThrowIfNull(opts);
        ArgumentNullException.ThrowIfNull(env);
        ArgumentException.ThrowIfNullOrWhiteSpace(azureServiceBusConnectionString);

        opts.Policies.DisableConventionalLocalRouting();
        opts.EnableAutomaticFailureAcks = false;
        opts.EnableRemoteInvocation = false;
        opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
        var azureBusConfig = opts
            .UseAzureServiceBus(azureServiceBusConnectionString)
            .AutoProvision();

        if (env.IsDevelopment())
        {
            azureBusConfig.AutoPurgeOnStartup();
        }

        return opts;
    }

    public static AzureServiceBusQueueListenerConfiguration ConfigureDeduplicatedQueueDefaults(
        this AzureServiceBusQueueListenerConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.ConfigureQueue(q =>
        {
            // NOTE! This can ONLY be set at queue creation time
            q.RequiresDuplicateDetection = true;

            // 20 seconds is the minimum allowed by ASB duplicate detection according to
            // https://learn.microsoft.com/en-us/azure/service-bus-messaging/duplicate-detection#duplicate-detection-window-size
            q.DuplicateDetectionHistoryTimeWindow = TimeSpan.FromSeconds(20);
        });
    }
}
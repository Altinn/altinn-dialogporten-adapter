using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.AzureServiceBus;

namespace Altinn.DialogportenAdapter.Contracts;

public static class WolverineOptionsExtentions
{
    public static WolverineOptions ConfigureAdapterDefaults(
        this WolverineOptions opts,
        IHostEnvironment env,
        string azureServiceBusConnectionString,
        string? managementConnectionString = null)
    {
        ArgumentNullException.ThrowIfNull(opts);
        ArgumentNullException.ThrowIfNull(env);
        ArgumentException.ThrowIfNullOrWhiteSpace(azureServiceBusConnectionString);

        opts.Policies.DisableConventionalLocalRouting();
        opts.EnableAutomaticFailureAcks = false;
        opts.EnableRemoteInvocation = false;
        opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
        if (!string.IsNullOrWhiteSpace(managementConnectionString))
        {
            var transport = opts.Transports.GetOrCreate<AzureServiceBusTransport>();
            transport.ManagementConnectionString = managementConnectionString;
        }
        var azureBusConfig = opts
            .UseAzureServiceBus(azureServiceBusConnectionString)
            .ConfigureSenders(s =>
            {
                s.CustomizeOutgoingMessagesOfType<SyncInstanceCommand>((envelope, _) =>
                {
                    // Duplication detection is enabled, which will cause retries within
                    // the duplication detection window to be silently discarded. Always
                    // setting a fresh id for the envelope circumvents this.
                    envelope.Id = Guid.NewGuid();
                });
            })
            .AutoProvision();

        if (env.IsDevelopment())
        {
            azureBusConfig.AutoPurgeOnStartup();
        }

        return opts;
    }
}

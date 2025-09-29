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
}
using System.Net;
using Altinn.ApiClients.Maskinporten.Interfaces;
using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.Integration.Tests.Common.Extensions;
using Altinn.DialogportenAdapter.Integration.Tests.Common.Services;
using Altinn.DialogportenAdapter.WebApi;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;
using Testcontainers.ServiceBus;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Wolverine;
using Wolverine.AzureServiceBus;
using Xunit;
using Request = WireMock.RequestBuilders.Request;

namespace Altinn.DialogportenAdapter.Integration.Tests.Common;

public class DialogportenAdapterApplication : IAsyncLifetime
{
    private INetwork _network = null!;
    private MsSqlContainer _mssqlContainer = null!;
    private ServiceBusContainer _asbContainer = null!;
    public WebApplication App { get; set; } = null!;
    private IHost StorageApp { get; set; } = null!;
    public IServiceScope AppScope { get; private set; } = null!;
    public IServiceScope StorageScope { get; private set; } = null!;
    public WireMockServer DialogportenApi { get; private set; } = null!;
    public WireMockServer AltinnApi { get; private set; } = null!;
    public WireMockServer StorageApi { get; private set; } = null!;
    public WireMockServer RegisterApi { get; private set; } = null!;
    public ServiceBusClient ServiceBusClient { get; private set; } = null!;
    private ServiceBusAdministrationClient ServiceBusAdminClient { get; set; } = null!;
    private static bool IsDebug => 
#if DEBUG
        true;
#else
    false;
#endif

    public async ValueTask InitializeAsync()
    {
        var networkAlias = "database-network";
        _network = new NetworkBuilder()
            .WithName(IsDebug ? "dialogporten-adapter-it-network" : null)
            .WithReuse(IsDebug)
            .Build();

        _mssqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")
            .WithNetwork(_network)
            .WithName(IsDebug ? "dialogporten-adapter-it-msql" : null)
            .WithReuse(IsDebug)
            .WithNetworkAliases(networkAlias)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("SQL Server is now ready for client connections"))
            .Build();

        var asbImage = "mcr.microsoft.com/azure-messaging/servicebus-emulator@" +
                       "sha256:a00c9626c8960f6b9be6178aa91a7ac8f1a102c0d9deda7603a1ba0ac9d9ab51";
        _asbContainer = new ServiceBusBuilder(asbImage)
            .WithAcceptLicenseAgreement(true)
            .WithName(IsDebug ? "dialogporten-adapter-it-asb" : null)
            .WithReuse(IsDebug)
            .WithMsSqlContainer(_network, _mssqlContainer, networkAlias)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Emulator Service is Successfully Up!"))
            .Build();

        await _mssqlContainer.StartAsync();
        await _asbContainer.StartAsync();

        ServiceBusClient = new ServiceBusClient(
            _asbContainer.GetConnectionString(),
            new ServiceBusClientOptions
            {
                RetryOptions =
                {
                    MaxRetries = 100,
                    Delay = TimeSpan.FromMilliseconds(250),
                    Mode = ServiceBusRetryMode.Fixed
                }
            }
        );
        ServiceBusAdminClient = new ServiceBusAdministrationClient(
            _asbContainer.GetHttpConnectionString(),
            new ServiceBusAdministrationClientOptions
            {
                Retry =
                {
                    MaxRetries = 100,
                    Delay = TimeSpan.FromMilliseconds(250),
                    Mode = RetryMode.Fixed
                }
            }
        );

        DialogportenApi = WireMockServer.Start().AddCustomFallbackMapping();
        AltinnApi = WireMockServer.Start().AddCustomFallbackMapping();
        StorageApi = WireMockServer.Start().AddCustomFallbackMapping();
        RegisterApi = WireMockServer.Start().AddCustomFallbackMapping();
        SetUpWireMockEndpointsForStartup();

        App = BuildDialogportenAdapterApplication();
        AppScope = App.Services.CreateScope();
        StorageApp = BuildStorageApplication();
        StorageScope = StorageApp.Services.CreateScope();

        await ClearBus();
        await Task.WhenAll(
            App.StartAsync(),
            StorageApp.StartAsync()
        );
    }

    public async ValueTask DisposeAsync()
    {
        var cleanup = new List<Task>
        {
            App.StopAsync(),
            StorageApp.StopAsync(),
            ServiceBusClient.DisposeAsync().AsTask(),
        };

        if (!IsDebug)
        {
            cleanup.AddRange(
                _asbContainer.DisposeAsync().AsTask(),
                _mssqlContainer.DisposeAsync().AsTask()
            );
        }

        await Task.WhenAll(cleanup);
        DialogportenApi.Stop();
        AltinnApi.Stop();
        StorageApi.Stop();
        RegisterApi.Stop();
        GC.SuppressFinalize(this);
    }

    private WebApplication BuildDialogportenAdapterApplication()
    {
        var builder = WebApplication.CreateBuilder();

        builder
            .Configuration
            .AddLocalDevelopmentSettings(builder.Environment);

        OverrideConfiguration(builder.Configuration);

        builder.Services
            .ConfigureDialogportenAdapterServices(builder.Configuration, builder.Environment, new QuickClock())
            .RemoveAll<IMaskinportenService>().AddTransient<IMaskinportenService, FakeMaskinportenService>();

        return builder.Build();
    }

    private IHost BuildStorageApplication()
    {
        var builder = Host.CreateDefaultBuilder();
        builder.UseWolverine(opts =>
        {
            opts
                .ConfigureAdapterDefaults(
                    App.Environment,
                    _asbContainer.GetConnectionString(),
                    _asbContainer.GetHttpConnectionString()
                )
                .PublishMessage<SyncInstanceCommand>()
                .ToAzureServiceBusQueue(Constants.AdapterQueueName);
        });
        return builder.Build();
    }

    private void OverrideConfiguration(ConfigurationManager builderConfiguration)
    {
        builderConfiguration["DialogportenAdapter:Dialogporten:BaseUri"] = DialogportenApi.Url;
        builderConfiguration["DialogportenAdapter:Altinn:BaseUri"] = AltinnApi.Url;
        builderConfiguration["DialogportenAdapter:Altinn:InternalStorageEndpoint"] = StorageApi.Url;
        builderConfiguration["DialogportenAdapter:Altinn:InternalRegisterEndpoint"] = RegisterApi.Url;
        builderConfiguration["WolverineSettings:ServiceBusConnectionString"] = _asbContainer.GetConnectionString();
        builderConfiguration["WolverineSettings:ManagementConnectionString"] = _asbContainer.GetHttpConnectionString();
    }

    private void SetUpWireMockEndpointsForStartup()
    {
        DialogportenApi
            .Given(Request.Create().UsingGet().WithPath("/api/v1/.well-known/jwks.json"))
            .AtPriority(short.MaxValue)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"keys\": []}"));
    }

    private async Task ClearBus()
    {
        var tasks = new List<Task>();

        await foreach (var queue in ServiceBusAdminClient.GetQueuesAsync())
        {
            tasks.Add(ServiceBusAdminClient.DeleteQueueAsync(queue.Name));
        }

        await Task.WhenAll(tasks);
    }
}

using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.EventSimulator;
using Altinn.DialogportenAdapter.EventSimulator.Common;
using Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;
using Altinn.DialogportenAdapter.EventSimulator.Common.StartupLoaders;
using Altinn.DialogportenAdapter.EventSimulator.Features.HistoryStream;
using Altinn.DialogportenAdapter.EventSimulator.Features.UpdateStream;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Adapter;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Persistance;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;
using Azure.Data.Tables;
using JasperFx;
using Microsoft.AspNetCore.Mvc;
using Refit;
using Wolverine;
using Wolverine.AzureServiceBus;
using Constants = Altinn.DialogportenAdapter.EventSimulator.Common.Constants;
using ContractConstants = Altinn.DialogportenAdapter.Contracts.Constants;

using var loggerFactory = CreateBootstrapLoggerFactory();
var bootstrapLogger = loggerFactory.CreateLogger<Program>();

try
{
    await BuildAndRun(args);
}
catch (Exception e)
{
    LogApplicationTerminatedUnexpectedly(bootstrapLogger, e);
    throw;
}

return;

static Task BuildAndRun(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging
        .ClearProviders()
        .AddConsole();

    builder.Configuration
        .AddCoreClusterSettings()
        .AddAzureKeyVault()
        .AddLocalDevelopmentSettings(builder.Environment);

    var settings = builder.Configuration.Get<Settings>()!;

    builder.Services.AddWolverine(opts =>
    {
        opts.ConfigureAdapterDefaults(
            builder.Environment,
            settings.WolverineSettings.ServiceBusConnectionString,
            settings.WolverineSettings.ManagementConnectionString
        );
        opts.Policies.AllListeners(x => x
            .ListenerCount(settings.WolverineSettings.ListenerCount)
            .ProcessInline());
        opts.Policies.AllSenders(x => x.SendInline());

        opts.ListenToAzureServiceBusQueue(ContractConstants.EventSimulatorQueueName);
        opts.PublishMessage<MigratePartitionCommand>()
            .ToAzureServiceBusQueue(ContractConstants.EventSimulatorQueueName);
        opts.PublishMessage<SyncInstanceCommand>()
            .ToAzureServiceBusQueue(ContractConstants.AdapterHistoryQueueName);

        // Do we need to use duplicate detection?
        // .ConfigureQueue(x => x.RequiresDuplicateDetection = true)
        // .AddOutgoingRule(new LambdaEnvelopeRule<SyncInstanceCommand>((e, m) => e.Id = m.InstanceId));
    });

    builder.Services.AddSingleton(settings);
    builder.Services.AddHostedService<InstanceUpdateStreamBackgroundService>();
    builder.Services.AddStartupLoaders();
    builder.Services.AddSingleton<IInstanceStreamer, InstanceStreamer>();
    builder.Services.AddTransient<MigrationPartitionService>();
    builder.Services.AddSingleton(_ => new TableClient(
        settings.DialogportenAdapter.AzureStorage.ConnectionString,
        AzureStorageSettings.GetTableName(builder.Environment), new TableClientOptions
        {
            Diagnostics =
            {
                IsLoggingContentEnabled = true,
                LoggedHeaderNames = { "x-ms-request-id", "x-ms-version" },
                LoggedQueryParameters = { "comp" }
            }
        }));
    builder.Services.AddSingleton<IMigrationPartitionRepository, MigrationPartitionRepository>();
    builder.Services.AddSingleton<IOrganizationRepository, OrganizationRepository>();
    // Health checks
    builder.Services.AddHealthChecks()
        .AddCheck<HealthCheck>("event_simulator_health_check");

    // Http clients
    builder.Services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(
        Constants.MaskinportenClientDefinitionKey,
        settings.DialogportenAdapter.Maskinporten);
    builder.Services.AddRefitClient<IStorageAdapterApi>()
        .ConfigureHttpClient(x =>
        {
            x.BaseAddress = settings.DialogportenAdapter.Adapter.InternalBaseUri;
            x.Timeout = Timeout.InfiniteTimeSpan;
        })
        .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(Constants.MaskinportenClientDefinitionKey);
    builder.Services.AddHttpClient(Constants.MaskinportenClientDefinitionKey)
        .ConfigureHttpClient(x => x.BaseAddress = settings.DialogportenAdapter.Altinn.ApiStorageEndpoint)
        .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(Constants.MaskinportenClientDefinitionKey);
    builder.Services.AddTransient<IStorageApi>(x => RestService
        .For<IStorageApi>(x.GetRequiredService<IHttpClientFactory>()
            .CreateClient(Constants.MaskinportenClientDefinitionKey)));

    builder.ReplaceLocalDevelopmentResources();

    var app = builder.Build();
    app.UseHttpsRedirection();
    app.MapHealthChecks("/health");
    app.MapOpenApi();
    app.MapPost("/api/migrate", (
            [FromBody] MigrationCommand command,
            [FromServices] MigrationPartitionService migrationPartitionService,
            CancellationToken cancellationToken) =>
        migrationPartitionService.Handle(command, cancellationToken));
    app.MapDelete("/api/table/truncate", (
            [FromServices] IMigrationPartitionRepository repo,
            CancellationToken cancellationToken) =>
        repo.Truncate(cancellationToken))
        .ExcludeFromDescription();

    return app.RunJasperFxCommands(args);
}

ILoggerFactory CreateBootstrapLoggerFactory() => LoggerFactory.Create(builder => builder
    .SetMinimumLevel(LogLevel.Warning)
    .AddSimpleConsole(options =>
    {
        options.IncludeScopes = true;
        options.UseUtcTimestamp = true;
        options.SingleLine = true;
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    }));

partial class Program
{
    [LoggerMessage(LogLevel.Critical, "Application terminated unexpectedly")]
    static partial void LogApplicationTerminatedUnexpectedly(ILogger<Program> logger, Exception exception);
}

﻿using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.DialogportenAdapter.EventSimulator;
using Altinn.DialogportenAdapter.EventSimulator.Common;
using Altinn.DialogportenAdapter.EventSimulator.Common.Channels;
using Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;
using Altinn.DialogportenAdapter.EventSimulator.Common.StartupLoaders;
using Altinn.DialogportenAdapter.EventSimulator.Features;
using Altinn.DialogportenAdapter.EventSimulator.Features.HistoryStream;
using Altinn.DialogportenAdapter.EventSimulator.Features.UpdateStream;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using Refit;

using var loggerFactory = CreateBootstrapLoggerFactory();
var bootstrapLogger = loggerFactory.CreateLogger<Program>();

try
{
    BuildAndRun(args);
}
catch (Exception e)
{
    bootstrapLogger.LogCritical(e, "Application terminated unexpectedly");
    throw;
}

return;

static void BuildAndRun(string[] args)
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

    builder.Services.AddSingleton(settings);
    builder.Services.AddChannelConsumer<InstanceEventConsumer, InstanceEvent>(consumers: 100);
    builder.Services.AddChannelConsumer<MigrationPartitionCommandConsumer, MigrationPartitionCommand>(consumers: 100);
    builder.Services.AddHostedService<InstanceUpdateStreamBackgroundService>();
    builder.Services.AddStartupLoaders();
    builder.Services.AddTransient<InstanceStreamer>();
    builder.Services.AddTransient<MigrationPartitionService>();
    builder.Services.AddSingleton(_ => new TableClient(
        settings.DialogportenAdapter.AzureStorage.ConnectionString,
        AzureStorageSettings.GetTableName(builder.Environment)));
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

    app.Run();
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
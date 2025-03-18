using Altinn.ApiClients.Dialogporten;
using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.DialogportenAdapter.WebApi;
using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Delete;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;
using Altinn.DialogportenAdapter.WebApi.Health;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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
        .AddAzureKeyVault();
    
    var settings = builder.Configuration.Get<Settings>()!;
    
    if (builder.Configuration.TryGetApplicationInsightsConnectionString(out var appInsightsConnectionString))
    {
        builder.Services
            .AddTransient<HealthCheckFilterProcessor>()
            .ConfigureOpenTelemetryTracerProvider((sp, builder) => builder.AddProcessor(sp.GetRequiredService<HealthCheckFilterProcessor>()))
            .AddOpenTelemetry()
            .ConfigureResource(x => x.AddAttributes([
                new("service.name", "platform-dialogporten-adapter")
            ]))
            .UseAzureMonitor(x =>
            {
                x.ConnectionString = appInsightsConnectionString;
                x.SamplingRatio = 0.05F;
                x.EnableLiveMetrics = false;
                x.StorageDirectory = "/tmp/logtelemetry";
            });
    }
    
    builder.Services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(
        Constants.DefaultMaskinportenClientDefinitionKey, 
        settings.DialogportenAdapter.Maskinporten);

    builder.Services
        .AddOpenApi()
        .AddSingleton(settings)
        .AddDialogportenClient(x =>
        {
            x.Maskinporten = settings.DialogportenAdapter.Maskinporten;
            x.BaseUri = settings.DialogportenAdapter.Dialogporten.BaseUri.ToString();
            x.ThrowOnPublicKeyFetchInit = false;
        })
        .AddTransient<SyncInstanceToDialogService>()
        .AddTransient<StorageDialogportenDataMerger>()
        .AddTransient<ActivityDtoTransformer>()
        .AddTransient<FourHundredLoggingDelegatingHandler>()
        
        // Http clients
        .AddRefitClient<IStorageApi>()
            .ConfigureHttpClient(x =>
            {
                x.BaseAddress = settings.DialogportenAdapter.Altinn.ApiStorageEndpoint;
                x.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", settings.DialogportenAdapter.Altinn.SubscriptionKey);
            })
            .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(Constants.DefaultMaskinportenClientDefinitionKey)
            .AddHttpMessageHandler<FourHundredLoggingDelegatingHandler>()
            .Services
        .AddRefitClient<IDialogportenApi>()
            .ConfigureHttpClient(x => x.BaseAddress = settings.DialogportenAdapter.Dialogporten.BaseUri)
            .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(Constants.DefaultMaskinportenClientDefinitionKey)
            .AddHttpMessageHandler<FourHundredLoggingDelegatingHandler>()
            .Services
            
        // Health checks
        .AddHealthChecks()
            .AddCheck<HealthCheck>("dialogporte_adapter_health_check");
    
    var app = builder.Build();

    app.UseHttpsRedirection();
    app.MapHealthChecks("/health");
    app.MapOpenApi();

    app.MapPost("/api/v1/syncDialog", async (
        [FromBody] SyncInstanceToDialogDto request,
        [FromServices] SyncInstanceToDialogService syncService,
        CancellationToken cancellationToken) =>
    {
        await syncService.Sync(request, cancellationToken);
        return Results.NoContent();
    });
    
    app.MapDelete("/api/v1/instance/{instanceOwner:int}/{instanceGuid:guid}", async (
        [FromRoute] int instanceOwner,
        [FromRoute] Guid instanceGuid,
        [FromQuery] bool hard,
        [FromHeader(Name = "Authorization")] string authorization,
        [FromServices] InstanceService instanceService,
        CancellationToken cancellationToken) =>
    {
        var request = new DeleteInstanceDto(instanceOwner, instanceGuid, hard, authorization);
        return await instanceService.Delete(request, cancellationToken) switch
        {
            DeleteInstanceResult.Success => Results.NoContent(),
            DeleteInstanceResult.InstanceNotFound => Results.NotFound(),
            DeleteInstanceResult.Unauthorized => Results.Unauthorized(),
            _ => Results.InternalServerError()
        };
    });
    
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
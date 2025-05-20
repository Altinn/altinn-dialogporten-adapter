using Altinn.ApiClients.Dialogporten;
using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.DialogportenAdapter.WebApi;
using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Common.Health;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Delete;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Refit;
using ZiggyCreatures.Caching.Fusion;

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

    builder.Services.AddMemoryCache();
    builder.Services.AddFusionCache()
        .WithDefaultEntryOptions(x =>
        {
            x.Duration = TimeSpan.FromMinutes(5);

            // Fail-safe options
            x.IsFailSafeEnabled = true;
            x.FailSafeMaxDuration = TimeSpan.FromHours(2);
            x.FailSafeThrottleDuration = TimeSpan.FromMinutes(30);

            // Factory timeouts
             x.FactorySoftTimeout = TimeSpan.FromSeconds(1);
            // Disabling hard timeouts as we don't want to handle SyntheticTimeoutException.
            // x.FactoryHardTimeout = TimeSpan.FromSeconds(2);
        });

    builder.Services
        .AddCors(x => x.AddDefaultPolicy(policy => policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()))
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.MetadataAddress = settings.DialogportenAdapter.Authentication.JwtBearerWellKnown;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(2)
                };
            })
            .Services
        .AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .Combine(options.FallbackPolicy)
                .RequireScope("altinn:storage/instances.syncadapter")
                .Build();
        })
        .AddOpenApi()
        .AddSingleton(settings)
        .AddDialogportenClient(x =>
        {
            x.Maskinporten = settings.DialogportenAdapter.Maskinporten;
            x.BaseUri = settings.DialogportenAdapter.Dialogporten.BaseUri.ToString();
            x.ThrowOnPublicKeyFetchInit = false;
        })
        .AddTransient<IRegisterRepository, NullRegisterRepository>()
        .AddTransient<SyncInstanceToDialogService>()
        .AddTransient<StorageDialogportenDataMerger>()
        .AddTransient<ActivityDtoTransformer>()
        .AddTransient<FourHundredLoggingDelegatingHandler>()
        .AddTransient<InstanceService>()

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
        .AddRefitClient<IRegisterApi>()
            .ConfigureHttpClient(x =>
            {
                x.BaseAddress = settings.DialogportenAdapter.Altinn.GetPlatformUri();
                x.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", settings.DialogportenAdapter.Altinn.SubscriptionKey);
            })
            .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(Constants.DefaultMaskinportenClientDefinitionKey)
            .AddHttpMessageHandler<FourHundredLoggingDelegatingHandler>()
            .Services

        // Health checks
        .AddHealthChecks()
            .AddCheck<HealthCheck>("dialogporte_adapter_health_check");

    builder.ReplaceLocalDevelopmentResources();

    var app = builder.Build();

    app.UseHttpsRedirection()
        .UseCors()
        .UseAuthentication()
        .UseAuthorization();

    var baseRoute = app.MapGroup("storage/dialogporten");
    var v1Route = baseRoute.MapGroup("api/v1");

    app.MapHealthChecks("/health")
        .AllowAnonymous();

    app.MapOpenApi()
        .AllowAnonymous();

    v1Route.MapPost("syncDialog", async (
        [FromBody] SyncInstanceToDialogDto request,
        [FromServices] SyncInstanceToDialogService syncService,
        CancellationToken cancellationToken) =>
    {
        await syncService.Sync(request, cancellationToken);
        return Results.NoContent();
    })
    .RequireAuthorization();

    v1Route.MapPost("syncDialog/simple/{partyId:int}/{instanceGuid:guid}", async (
            [FromRoute] int partyId,
            [FromRoute] Guid instanceGuid,
            [FromQuery] bool? isMigration,
            [FromServices] SyncInstanceToDialogService syncService,
            [FromServices] IStorageApi storageApi,
            CancellationToken cancellationToken) =>
        {
            var instance = await storageApi
                .GetInstance(partyId, instanceGuid, cancellationToken)
                .ContentOrDefault();
            if (instance is null)
            {
                return Results.NotFound();
            }

            var request = new SyncInstanceToDialogDto(instance.AppId, partyId, instanceGuid, instance.Created!.Value, isMigration ?? false);
            await syncService.Sync(request, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .ExcludeFromDescription();

    v1Route.MapDelete("instance/{instanceOwner:int}/{instanceGuid:guid}", async (
            [FromRoute] int instanceOwner,
            [FromRoute] Guid instanceGuid,
            [FromHeader(Name = "Authorization")] string authorization,
            [FromServices] InstanceService instanceService,
            CancellationToken cancellationToken) =>
        {
            var request = new DeleteInstanceDto(instanceOwner, instanceGuid, authorization);
            return await instanceService.Delete(request, cancellationToken) switch
            {
                DeleteInstanceResult.Success => Results.NoContent(),
                DeleteInstanceResult.InstanceNotFound => Results.NotFound(),
                DeleteInstanceResult.Unauthorized => Results.Unauthorized(),
                _ => Results.InternalServerError()
            };
        })
        .AllowAnonymous(); // 👈 Dialog token is validated inside InstanceService

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
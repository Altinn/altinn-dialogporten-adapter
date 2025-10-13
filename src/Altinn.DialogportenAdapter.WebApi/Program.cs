using System.Diagnostics;
using System.Net;
using Altinn.ApiClients.Dialogporten;
using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.DialogportenAdapter.Contracts;
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
using JasperFx.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Refit;
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.ErrorHandling;
using Wolverine.Runtime;
using ZiggyCreatures.Caching.Fusion;
using Constants = Altinn.DialogportenAdapter.WebApi.Common.Constants;
using ContractConstants = Altinn.DialogportenAdapter.Contracts.Constants;

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
    builder.Services.AddOptions<Settings>().Bind(builder.Configuration);

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

    builder.Services.AddWolverine(opts =>
    {
        opts.ConfigureAdapterDefaults(builder.Environment,
            settings.WolverineSettings.ServiceBusConnectionString);
        opts.Policies.AllListeners(x => x
            .ProcessInline());
        opts.Policies.AllSenders(x => x.SendInline());

        // Handle 410, which we get when trying to DELETE an already deleted dialog. Just discard.
        opts.Policies
            .OnException<ApiException>(ex => ex.StatusCode is HttpStatusCode.Gone)
            .Discard();

        // If the queue backlog grows faster than we can drain it (ie. due to downtime), two messages with the same
        // InstanceId may still be processed concurrently once we catch up, causing dialog
        // update conflicts. Ie. the (somewhat theoretical) scenario:
        // 1. An update message is sent at time X
        // 2. Adapter process A fetches the instance at time X and starts processing
        // 3. Another update message is sent at time X+1
        // 4. Adapter process B fetches the instance at time X+1 and starts processing
        // 5. Process A finishes and commits new dialog (new revision)
        // 6. Process B finishes and tries to update dialog, but gets a 412
        opts.Policies
            .OnException<ApiException>(ex => ex.StatusCode
                is HttpStatusCode.PreconditionFailed)
            .RetryWithJitteredCooldown(1.Seconds(), 3.Seconds(), 5.Seconds())
            .Then.MoveToErrorQueue();

        // 422s may be caused by concurrency issues (conflicting creates with same IDs), so we initially retry a few times with jitter.
        // It may however also be caused by caching issues, ie. when a very recent service resource is referred which we have not
        // yet cached, or a very new organization. In those cases, we need to reschedule the message for later processing to give time for caches to expire.
        opts.Policies
            .OnException<ApiException>(ex => ex.StatusCode
                is HttpStatusCode.UnprocessableEntity)
            .RetryWithJitteredCooldown(1.Seconds(), 3.Seconds(), 5.Seconds())
            .Then.ScheduleRetry(10.Seconds(), 30.Seconds(), 1.Minutes(), 5.Minutes(), 5.Minutes(), 10.Minutes())
            .Then.MoveToErrorQueue();


        // Attempt to handle errors most likely caused by expired/invalid tokens. If retries don't help, move to error queue for manual inspection.
        opts.Policies
            .OnException<ApiException>(ex => ex.StatusCode is HttpStatusCode.Unauthorized)
            .RetryWithJitteredCooldown(1.Seconds(), 3.Seconds(), 5.Seconds())
            .Then.MoveToErrorQueue();

        // 5xx errors are usually transient (upstream being down/overloaded), so try a few times with a cooldown before
        // re-scheduling indefinitely. HttpRequestExceptions indicates network issues, DNS issues, etc. which are usually
        // transient, so handle this the same as 5xx errors.
        opts.Policies.OnException<HttpRequestException>()
            .Or<ApiException>(x => (int)x.StatusCode >= 500)
            .OrInner<ApiException>(x => (int)x.StatusCode >= 500)
            .RetryWithCooldown(10.Seconds(), 20.Seconds())
            .Then.ScheduleRetryIndefinitely(30.Seconds(), 60.Seconds(), 2.Minutes())
            .AndPauseProcessing(30.Seconds()); // Give some time for upstream to recover before processing more messages

        opts.ListenToAzureServiceBusQueue(ContractConstants.AdapterQueueName)
            .ListenerCount(70.PercentOf(settings.WolverineSettings.ListenerCount));

        // Also listen to the history queue with a fewer number of listeners.
        opts.ListenToAzureServiceBusQueue(ContractConstants.AdapterHistoryQueueName)
            .ListenerCount(30.PercentOf(settings.WolverineSettings.ListenerCount));

    });

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
        .AddDialogportenClient(x =>
        {
            x.Maskinporten = settings.DialogportenAdapter.Maskinporten;
            x.BaseUri = settings.DialogportenAdapter.Dialogporten.BaseUri.ToString();
            x.ThrowOnPublicKeyFetchInit = false;
        })
        .AddTransient<IRegisterRepository, RegisterRepository>()
        .AddTransient<ISyncInstanceToDialogService, SyncInstanceToDialogService>()
        .AddTransient<StorageDialogportenDataMerger>()
        .AddTransient<ActivityDtoTransformer>()
        .AddTransient<FourHundredLoggingDelegatingHandler>()
        .AddTransient<InstanceService>()

        // Http clients
        .AddRefitClient<IStorageApi>()
            .ConfigureHttpClient(x =>
            {
                x.BaseAddress = settings.DialogportenAdapter.Altinn.InternalStorageEndpoint;
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
                x.BaseAddress = settings.DialogportenAdapter.Altinn.InternalRegisterEndpoint;
                x.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", settings.DialogportenAdapter.Altinn.SubscriptionKey);
            })
            .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(Constants.DefaultMaskinportenClientDefinitionKey)
            .AddHttpMessageHandler<FourHundredLoggingDelegatingHandler>()
            .Services

        // Health checks
        .AddHealthChecks()
            .AddCheck<HealthCheck>("dialogporte_adapter_health_check");

    builder.ReplaceLocalDevelopmentResources();

    using var app = builder.Build();

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
        [FromBody] SyncInstanceCommand request,
        [FromServices] ISyncInstanceToDialogService syncService,
        CancellationToken cancellationToken) =>
    {
        await syncService.Sync(request, cancellationToken);
        return Results.NoContent();
    })
    .RequireAuthorization();

    v1Route.MapPost("syncDialog/simple/{partyId}/{instanceGuid:guid}", async (
            [FromRoute] string partyId,
            [FromRoute] Guid instanceGuid,
            [FromQuery] bool? isMigration,
            [FromServices] ISyncInstanceToDialogService syncService,
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

            var request = new SyncInstanceCommand(instance.AppId, partyId, instanceGuid, instance.Created!.Value, isMigration ?? false);
            await syncService.Sync(request, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .ExcludeFromDescription();

    v1Route.MapDelete("instance/{instanceOwner}/{instanceGuid:guid}", async (
            [FromRoute] string instanceOwner,
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

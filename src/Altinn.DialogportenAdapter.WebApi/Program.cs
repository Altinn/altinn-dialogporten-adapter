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
            .ListenerCount(settings.WolverineSettings.ListenerCount)
            .ProcessInline());
        opts.Policies.AllSenders(x => x.SendInline());

        // NOTE! The queue is using duplicate detection with a 20-second window, which means any re-queueing within
        // that window will be treated as a duplicate and the message will be silently dropped by ASB.
        // This means that the retry attempts here must be spaced out to exceed that window.
        //
        // By default, Wolverine will immediately send a message to the error queue if an exception is thrown, unless
        // it is handled by a policy below.

        // Handle 410, which we get when trying to DELETE an already deleted dialog. Just discard.
        opts.Policies.OnException<ApiException>(ex =>
                ex.StatusCode is HttpStatusCode.Gone)
            .Discard();

        // Handle 412 which is caused by timing/duplicate messages. Try a few times, then reschedule at the end of the
        // next time bucket of 1 minute duration (ie. a message handled at 13:00:00, 13:00:47 or 13:00:59 will be
        // scheduled to 13:01:59. Placing them within the same de-duplication window will reduce the number of duplicates to deal with.
        opts.Policies.OnException<ApiException>(ex =>
                ex.StatusCode is HttpStatusCode.PreconditionFailed)
            .RetryWithCooldown(500.Milliseconds(), 1.Seconds(), 3.Seconds(), 5.Seconds(), 10.Seconds()) // Must in total exceed ASB duplicate detection window
            .Then.Discard().And<RetryAtEndOfBucket>();

        // Handle 422 errors, which may be caused by timing/duplicate messages, but might also be other issues, so try a few times
        // then move to error queue for manual inspection.
        opts.Policies.OnException<ApiException>(ex =>
                ex.StatusCode is HttpStatusCode.UnprocessableEntity)
            .RetryWithCooldown(500.Milliseconds(), 1.Seconds(), 3.Seconds(), 5.Seconds(), 10.Seconds())
            .Then.MoveToErrorQueue();

        // 5xx errors are usually transient (upstream being down/overloaded), so try a few times with a cooldown to ensure we're moved
        // beyond the ASB duplicate detection window before re-scheduling indefinitely. HttpRequestExceptions indicates
        // network issues, DNS issues, etc. which are usually transient, so handle this the same as 5xx errors.
        opts.Policies.OnException(ex => ex switch
            {
                HttpRequestException => true,
                ApiException apiException when (int)apiException.StatusCode >= 500 => true,
                AggregateException aggregateException when aggregateException.Flatten().InnerExceptions.Any(e =>
                    e is ApiException ae && (int)ae.StatusCode >= 500) => true,
                _ => false
            })
            .RetryWithCooldown(10.Seconds(), 20.Seconds()) // Must in total exceed ASB duplicate detection window
            .Then.ScheduleRetryIndefinitely(30.Seconds(), 60.Seconds(), 2.Minutes())
            .AndPauseProcessing(30.Seconds()); // Give some time for upstream to recover before processing more messages

        opts.ListenToAzureServiceBusQueue(ContractConstants.AdapterQueueName)
            .ConfigureQueue(q =>
            {
                // NOTE! This can ONLY be set at queue creation time
                q.RequiresDuplicateDetection = true;

                // 20 seconds is the minimum allowed by ASB duplicate detection according to
                // https://learn.microsoft.com/en-us/azure/service-bus-messaging/duplicate-detection#duplicate-detection-window-size
                q.DuplicateDetectionHistoryTimeWindow = 20.Seconds();
            });
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
        .AddSingleton(settings)
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
                x.DefaultRequestHeaders.Add("User-Agent", settings.DialogportenAdapter.UserAgent);
            })
            .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(Constants.DefaultMaskinportenClientDefinitionKey)
            .AddHttpMessageHandler<FourHundredLoggingDelegatingHandler>()
            .Services
        .AddRefitClient<IDialogportenApi>()
            .ConfigureHttpClient(x =>
            {
                x.BaseAddress = settings.DialogportenAdapter.Dialogporten.BaseUri;
                x.DefaultRequestHeaders.Add("User-Agent", settings.DialogportenAdapter.UserAgent);
            })
            .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(Constants.DefaultMaskinportenClientDefinitionKey)
            .AddHttpMessageHandler<FourHundredLoggingDelegatingHandler>()
            .Services
        .AddRefitClient<IRegisterApi>()
            .ConfigureHttpClient(x =>
            {
                x.BaseAddress = settings.DialogportenAdapter.Altinn.InternalRegisterEndpoint;
                x.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", settings.DialogportenAdapter.Altinn.SubscriptionKey);
                x.DefaultRequestHeaders.Add("User-Agent", settings.DialogportenAdapter.UserAgent);
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


internal sealed class RetryAtEndOfBucket() : UserDefinedContinuation("Retry at end of next bucket")
{
    private readonly TimeSpan _bucket = TimeSpan.FromMinutes(1);

    public override async ValueTask ExecuteAsync(
        IEnvelopeLifecycle lifecycle, IWolverineRuntime runtime,
        DateTimeOffset now, Activity? activity)
    {
        var utcTicks = now.UtcTicks;
        var bucketStartUtcTicks = utcTicks - (utcTicks % _bucket.Ticks);
        var bucketStartUtc = new DateTimeOffset(bucketStartUtcTicks, TimeSpan.Zero);
        var bucketEndUtc = bucketStartUtc + _bucket * 2 - TimeSpan.FromMilliseconds(1);

        await lifecycle.ReScheduleAsync(bucketEndUtc);
    }
}

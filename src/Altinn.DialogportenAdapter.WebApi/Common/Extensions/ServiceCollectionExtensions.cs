using System.Net;
using Altinn.ApiClients.Dialogporten;
using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.WebApi.Common.Health;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Delete;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Refit;
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.ErrorHandling;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Locking.AsyncKeyed;
using ContractConstants = Altinn.DialogportenAdapter.Contracts.Constants;

namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection ConfigureDialogportenAdapterServices(
            IConfiguration configuration,
            IHostEnvironment environment,
            IClock clock)
        {
            services.AddOptions<Settings>().Bind(configuration);

            services
                .AddInfrastructure(configuration, environment, clock)
                .AddHttpClients(configuration)
                .AddWebApiConfiguration(configuration)
                .AddApplicationServices(clock);

            return services;
        }

        private IServiceCollection AddInfrastructure(
            IConfiguration configuration,
            IHostEnvironment environment,
            IClock clock)
        {
            var settings = configuration.Get<Settings>()!;
            if (configuration.TryGetApplicationInsightsConnectionString(out var appInsightsConnectionString))
            {
                services
                    .AddTransient<HealthCheckFilterProcessor>()
                    .ConfigureOpenTelemetryTracerProvider((sp, builder) =>
                        builder.AddProcessor(sp.GetRequiredService<HealthCheckFilterProcessor>()))
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

            services.AddWolverine(opts =>
            {
                opts.ConfigureAdapterDefaults(
                    environment,
                    settings.WolverineSettings.ServiceBusConnectionString,
                    settings.WolverineSettings.ManagementConnectionString
                );
                opts.Policies.AllListeners(x => x.ProcessInline());
                opts.Policies.AllSenders(x => x.SendInline());

                // Handle 409s, which may be caused by conflicting creates with same IDs. Retry
                // a few times with jitter before moving to error queue for manual inspection.
                // This may happen during migrations when the same instance is attempted created
                // multiple times.
                opts.Policies
                    .OnException<ApiException>(ex => ex.StatusCode is HttpStatusCode.Conflict)
                    .RetryWithJitteredCooldown(clock.Seconds(1), clock.Seconds(3), clock.Seconds(5))
                    .Then.MoveToErrorQueue();

                // Handle 410, which we get when trying to DELETE an already deleted dialog. Just discard.
                opts.Policies
                    .OnException<ApiException>(ex => ex.StatusCode is HttpStatusCode.Gone)
                    .OrAnyInner<ApiException>(ex => ex.StatusCode is HttpStatusCode.Gone)
                    .CustomActionIndefinitely((_, lifetime, _) => lifetime.CompleteAsync(), "Discard indefinitely");

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
                    .OnException<ApiException>(ex => ex.StatusCode is HttpStatusCode.PreconditionFailed)
                    .RetryWithJitteredCooldown(clock.Seconds(1), clock.Seconds(3), clock.Seconds(5))
                    .Then.MoveToErrorQueue();

                // 422s may be caused by concurrency issues (conflicting creates with same IDs), so we initially retry a few times with jitter.
                // It may however also be caused by caching issues, ie. when a very recent service resource is referred which we have not
                // yet cached, or a very new organization. In those cases, we need to reschedule the message for later processing to give time for caches to expire.
                opts.Policies
                    .OnException<ApiException>(ex => ex.StatusCode is HttpStatusCode.UnprocessableEntity)
                    .RetryWithJitteredCooldown(clock.Seconds(1), clock.Seconds(3), clock.Seconds(5))
                    .Then.ScheduleRetry(clock.Seconds(10), clock.Seconds(30), clock.Minutes(1), clock.Minutes(5),
                        clock.Minutes(5), clock.Minutes(10))
                    .Then.MoveToErrorQueue();

                // PartyNotFoundExceptions may happen due to desyncs between Altinn 2 and Altinn 3 register. Reschedule for a retry after a while,
                // eventually failing to error queue for manual inspection if the party is still not found.

                opts.Policies
                    .OnException<PartyNotFoundException>()
                    .OrAnyInner<PartyNotFoundException>()
                    .RetryWithJitteredCooldown(clock.Seconds(1), clock.Seconds(5), clock.Seconds(20))
                    .Then.ScheduleRetry(clock.Minutes(1), clock.Minutes(10), clock.Minutes(30))
                    .Then.MoveToErrorQueue();

                // Attempt to handle errors most likely caused by expired/invalid tokens. If retries don't help, move to error queue for manual inspection.
                opts.Policies
                    .OnException<ApiException>(ex => ex.StatusCode is HttpStatusCode.Unauthorized)
                    .OrAnyInner<ApiException>(ex => ex.StatusCode is HttpStatusCode.Unauthorized)
                    .RetryWithJitteredCooldown(clock.Seconds(1), clock.Seconds(3), clock.Seconds(5))
                    .Then.MoveToErrorQueue();

                // 501 NotImplemented errors shouldn't ever occur. If they do => immediately move to dlq
                // Used as a catch-all "net" for integration tests
                opts.Policies
                    .OnException<ApiException>(x => (int)x.StatusCode == 501)
                    .OrAnyInner<ApiException>(x => (int)x.StatusCode == 501)
                    .MoveToErrorQueue();

                // 5xx errors are usually transient (upstream being down/overloaded), so try a few times with a cooldown before
                // re-scheduling indefinitely, as is timeouts (TaskCanceledException). HttpRequestExceptions indicates network issues, DNS issues, etc. which are usually
                // transient, so handle this the same as 5xx errors.
                opts.Policies
                    .OnException<HttpRequestException>()
                    .Or<ApiException>(x => (int)x.StatusCode >= 500)
                    .OrAnyInner<ApiException>(x => (int)x.StatusCode >= 500)
                    .Or<TaskCanceledException>()
                    .RetryWithCooldown(clock.Seconds(10), clock.Seconds(20))
                    .Then.ScheduleRetryIndefinitely(clock.Seconds(30), clock.Seconds(60), clock.Minutes(2));

                // Disabled, as bugs is any upstreams might cause a soft head-of-line-blocking,
                // where everything gets delayed due to a few problematic messages at the front of the queue.
                // It's better to have some failed messages in the error queue and keep processing the rest
                // of the queue than to have everything delayed due to a few problematic messages.
                // Detecting actual overload situations and pausing the queue processing temporarily is not trivial,
                // and would require more advanced monitoring and alerting setup to do properly.

                //.AndPauseProcessing(clock.Seconds(30)); // Give some time for upstream to recover before processing more messages

                opts.ListenToAzureServiceBusQueue(ContractConstants.AdapterQueueName)
                    .ListenerCount(70.PercentOf(settings.WolverineSettings.ListenerCount));

                // Also listen to the history queue with a fewer number of listeners.
                opts.ListenToAzureServiceBusQueue(ContractConstants.AdapterHistoryQueueName)
                    .ListenerCount(30.PercentOf(settings.WolverineSettings.ListenerCount));
            });

            services.AddMemoryCache();
            services.AddFusionCache()
                .WithMemoryLocker(new AsyncKeyedMemoryLocker())
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

            // Health checks
            services
                .AddHealthChecks()
                .AddCheck<HealthCheck>("dialogporte_adapter_health_check");

            return services;
        }

        private IServiceCollection AddHttpClients(IConfiguration configuration)
        {
            var settings = configuration.Get<Settings>()!;
            var clientKey = Constants.DefaultMaskinportenClientDefinitionKey;

            services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(
                clientKey,
                settings.DialogportenAdapter.Maskinporten
            );

            services.AddDialogportenClient(x =>
            {
                x.Maskinporten = settings.DialogportenAdapter.Maskinporten;
                x.BaseUri = settings.DialogportenAdapter.Dialogporten.BaseUri.ToString();
                x.ThrowOnPublicKeyFetchInit = false;
            });

            services.AddRefitClient<IStorageApi>()
                .ConfigureHttpClient(x =>
                {
                    x.BaseAddress = settings.DialogportenAdapter.Altinn.InternalStorageEndpoint;
                    x.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key",
                        settings.DialogportenAdapter.Altinn.SubscriptionKey);
                })
                .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(clientKey)
                .AddHttpMessageHandler<FourHundredLoggingDelegatingHandler>();

            services
                .AddRefitClient<IApplicationsApi>()
                .ConfigureHttpClient(x =>
                {
                    x.BaseAddress = settings.DialogportenAdapter.Altinn.InternalStorageEndpoint;
                    x.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key",
                        settings.DialogportenAdapter.Altinn.SubscriptionKey);
                })
                .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(clientKey)
                .AddHttpMessageHandler<FourHundredLoggingDelegatingHandler>();

            services
                .AddRefitClient<IDialogportenApi>()
                .ConfigureHttpClient(x => x.BaseAddress = settings.DialogportenAdapter.Dialogporten.BaseUri)
                .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(clientKey)
                .AddHttpMessageHandler<FourHundredLoggingDelegatingHandler>();

            services
                .AddRefitClient<IRegisterApi>()
                .ConfigureHttpClient(x =>
                {
                    x.BaseAddress = settings.DialogportenAdapter.Altinn.InternalRegisterEndpoint;
                    x.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key",
                        settings.DialogportenAdapter.Altinn.SubscriptionKey);
                })
                .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(clientKey)
                .AddHttpMessageHandler<FourHundredLoggingDelegatingHandler>();

            return services;
        }

        private IServiceCollection AddWebApiConfiguration(IConfiguration configuration)
        {
            var settings = configuration.Get<Settings>()!;

            return services
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
                .AddOpenApi();
        }

        private IServiceCollection AddApplicationServices(IClock clock)
        {
            return services
                .AddSingleton<IClock>(_ => clock)
                .AddTransient<IRegisterRepository, RegisterRepository>()
                .AddTransient<IApplicationRepository, ApplicationRepository>()
                .AddTransient<ISyncInstanceToDialogService, SyncInstanceToDialogService>()
                .AddTransient<StorageDialogportenDataMerger>()
                .AddTransient<ActivityDtoTransformer>()
                .AddTransient<FourHundredLoggingDelegatingHandler>()
                .AddTransient<InstanceService>();
        }

        public IServiceCollection ReplaceLocalDevelopmentResources(
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            if (!environment.IsDevelopment() ||
                !configuration.TryGetLocalDevelopmentSettings(out var opt))
            {
                return services;
            }

            services
                .DoIf(opt.MockDialogportenApi,
                    x => x.Replace<IDialogportenApi, MockDialogportenApi>(ServiceLifetime.Transient))
                .DoIf(opt.DisableAuth,
                    x => x.Replace<IAuthorizationHandler, AllowAnonymousHandler>(ServiceLifetime.Singleton));

            return services;
        }

        private IServiceCollection DoIf(bool predicate, Action<IServiceCollection> action)
        {
            if (predicate) action(services);
            return services;
        }

        private IServiceCollection Replace<TService, TImplementation>(
            ServiceLifetime lifetime)
            where TService : class
            where TImplementation : class, TService
        {
            var serviceType = typeof(TService);
            var implementationType = typeof(TImplementation);
            // Remove all matching service registrations
            for (var i = services.Count - 1; i >= 0; i--)
            {
                if (services[i].ServiceType == serviceType) services.RemoveAt(i);
            }

            services.Add(ServiceDescriptor.Describe(serviceType, implementationType, lifetime));
            return services;
        }
    }
}

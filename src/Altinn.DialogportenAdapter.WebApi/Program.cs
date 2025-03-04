using Altinn.ApiClients.Dialogporten;
using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.DialogportenAdapter.WebApi;
using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Configuration;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Delete;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;
using Altinn.DialogportenAdapter.WebApi.Health;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Notifications.Configuration;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Refit;

ILogger logger;

const string defaultMaskinportenClientDefinitionKey = "DefaultMaskinportenClientDefinitionKey";
const string vaultApplicationInsightsKey = "ApplicationInsights--InstrumentationKey";

string applicationInsightsConnectionString = string.Empty;

var builder = WebApplication.CreateBuilder(args);

var settings = builder.Configuration.Get<Settings>()!;

ConfigureWebHostCreationLogging();

await SetConfigurationProviders(builder.Configuration);

builder.Logging.ConfigureApplicationLogging(applicationInsightsConnectionString);

builder.Services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(
    defaultMaskinportenClientDefinitionKey, 
    settings.DialogportenAdapter.Maskinporten);

builder.Services
    .AddOpenApi()
    .AddSingleton(settings)
    .AddDialogportenClient(x =>
    {
        x.Maskinporten = settings.DialogportenAdapter.Maskinporten;
        x.BaseUri = settings.DialogportenAdapter.Dialogporten.BaseUri.ToString();
    })
    .AddTransient<SyncInstanceToDialogService>()
    .AddTransient<StorageDialogportenDataMerger>()
    .AddTransient<ActivityDtoTransformer>()
    
    // Http clients
    .AddRefitClient<IStorageApi>()
        .ConfigureHttpClient(x =>
        {
            x.BaseAddress = settings.DialogportenAdapter.Altinn.ApiStorageEndpoint;
            x.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", settings.DialogportenAdapter.Altinn.SubscriptionKey);
        })
        .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(defaultMaskinportenClientDefinitionKey)
        .Services
    .AddRefitClient<IDialogportenApi>()
        .ConfigureHttpClient(x => x.BaseAddress = settings.DialogportenAdapter.Dialogporten.BaseUri)
        .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(defaultMaskinportenClientDefinitionKey)
        .Services
        
    // Health checks
    .AddHealthChecks()
        .AddCheck<HealthCheck>("dialogporte_adapter_health_check");


if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
{
    builder.Services.AddSingleton(typeof(ITelemetryChannel), new ServerTelemetryChannel { StorageFolder = "/tmp/logtelemetry" });
    builder.Services.AddApplicationInsightsTelemetry(new ApplicationInsightsServiceOptions
    {
        ConnectionString = applicationInsightsConnectionString
    });

    builder.Services.AddApplicationInsightsTelemetryProcessor<HealthTelemetryFilter>();
    builder.Services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();
}
var app = builder.Build();

app.MapHealthChecks("/health");
app.MapOpenApi();
app.UseHttpsRedirection();

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

void ConfigureWebHostCreationLogging()
{
    var logFactory = LoggerFactory.Create(logBuilder =>
    {
        logBuilder
            .AddFilter("Altinn.DialogportenAdapter.WebApi.Program", LogLevel.Debug)
            .AddConsole();
    });

    logger = logFactory.CreateLogger<Program>();
}

async Task SetConfigurationProviders(ConfigurationManager config)
{
    if (Directory.Exists("/altinn-appsettings"))
    {
        logger.LogWarning("Reading altinn-dbsettings-secret.json.");
        IFileProvider fileProvider = new PhysicalFileProvider("/altinn-appsettings");
        config.AddJsonFile(fileProvider, "altinn-dbsettings-secret.json", optional: true, reloadOnChange: true);
    }
    else
    {
        logger.LogWarning("Expected directory \"/altinn-appsettings\" not found.");
    }

    await ConnectToKeyVaultAndSetApplicationInsights(config);
}

async Task ConnectToKeyVaultAndSetApplicationInsights(ConfigurationManager config)
{
    KeyVaultSettings keyVaultSettings = new();
    config.GetSection("kvSetting").Bind(keyVaultSettings);
    if (!string.IsNullOrEmpty(keyVaultSettings.ClientId) &&
        !string.IsNullOrEmpty(keyVaultSettings.TenantId) &&
        !string.IsNullOrEmpty(keyVaultSettings.ClientSecret) &&
        !string.IsNullOrEmpty(keyVaultSettings.SecretUri))
    {
        logger.LogInformation("Program // Configure key vault client // App");
        Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", keyVaultSettings.ClientId);
        Environment.SetEnvironmentVariable("AZURE_CLIENT_SECRET", keyVaultSettings.ClientSecret);
        Environment.SetEnvironmentVariable("AZURE_TENANT_ID", keyVaultSettings.TenantId);
        var azureCredentials = new DefaultAzureCredential();

        config.AddAzureKeyVault(new Uri(keyVaultSettings.SecretUri), azureCredentials);

        SecretClient client = new(new Uri(keyVaultSettings.SecretUri), azureCredentials);

        try
        {
            KeyVaultSecret keyVaultSecret = await client.GetSecretAsync(vaultApplicationInsightsKey);
            applicationInsightsConnectionString = string.Format("InstrumentationKey={0}", keyVaultSecret.Value);
        }
        catch (Exception vaultException)
        {
            logger.LogError(vaultException, "Unable to read application insights key.");
        }
    }
}
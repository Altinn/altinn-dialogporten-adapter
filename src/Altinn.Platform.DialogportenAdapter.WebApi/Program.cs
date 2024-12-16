using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.Platform.DialogportenAdapter.WebApi;
using Altinn.Platform.DialogportenAdapter.WebApi.Features.Command.Delete;
using Altinn.Platform.DialogportenAdapter.WebApi.Features.Command.Sync;
using Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Refit;

const string defaultMaskinportenClientDefinitionKey = "DefaultMaskinportenClientDefinitionKey";

var builder = WebApplication.CreateBuilder(args);

var settings = builder.Configuration.Get<Settings>()!;

builder.Services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(
    defaultMaskinportenClientDefinitionKey, 
    settings.Infrastructure.Maskinporten);

builder.Services.AddOpenApi()
    .AddSingleton(settings)
    .AddTransient<SyncInstanceToDialogService>()
    .AddTransient<StorageDialogportenDataMerger>()
    
    // Http clients
    .AddRefitClient<IStorageApi>()
        .ConfigureHttpClient(x =>
        {
            x.BaseAddress = settings.Infrastructure.Altinn.PlatformBaseUri;
            x.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", settings.Infrastructure.Altinn.SubscriptionKey);
        })
        .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(defaultMaskinportenClientDefinitionKey)
        .Services
    .AddRefitClient<IDialogportenApi>()
        .ConfigureHttpClient(x => x.BaseAddress = settings.Infrastructure.Dialogporten.BaseUri)
        .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(defaultMaskinportenClientDefinitionKey);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/api/v1/syncDialog", async (
    [FromBody] SyncInstanceToDialogDto request,
    [FromServices] SyncInstanceToDialogService syncService,
    CancellationToken cancellationToken) =>
{
    await syncService.Sync(request, cancellationToken);
    return Results.NoContent();
});

app.MapDelete("/api/v1/instance/{instanceId}", async (
    [FromRoute] string instanceId,
    [FromQuery] bool hard,
    [FromServices] DeleteDialogService deleteService,
    CancellationToken cancellationToken) =>
{
    var request = new DeleteDialogDto(instanceId, hard);
    await deleteService.DeleteDialog(request, cancellationToken);
    return Results.NoContent();
});
app.Run();
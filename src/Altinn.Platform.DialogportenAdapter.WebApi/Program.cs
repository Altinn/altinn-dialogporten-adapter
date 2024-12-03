using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.Platform.DialogportenAdapter.WebApi;
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
    .AddTransient<SyncInstanceToDialogService>()
    
    // Http clients
    .AddRefitClient<IStorageApi>()
        .ConfigureHttpClient(x =>
        {
            x.BaseAddress = settings.Infrastructure.Altinn.BaseUri;
            x.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", settings.Infrastructure.Altinn.SubscriptionKey);
        })
        .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(defaultMaskinportenClientDefinitionKey)
        .Services
    .AddRefitClient<IDialogportenApi>()
        .ConfigureHttpClient(x => x.BaseAddress = new Uri("https://altinn-dev-api.azure-api.net/dialogporten"))
        .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(defaultMaskinportenClientDefinitionKey);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapPost("/syncDialog", async (
    [FromBody] SyncInstanceToDialogDto request,
    [FromServices] SyncInstanceToDialogService syncService,
    CancellationToken cancellationToken) =>
{
    var result = await syncService.Sync(request, cancellationToken);
    return Results.Ok(result);
});
app.Run();
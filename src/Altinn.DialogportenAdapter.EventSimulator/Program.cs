﻿using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.DialogportenAdapter.EventSimulator;
using Altinn.DialogportenAdapter.EventSimulator.Common;
using Altinn.DialogportenAdapter.EventSimulator.Features;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Refit;

const string defaultMaskinportenClientDefinitionKey = "DefaultMaskinportenClientDefinitionKey";

var builder = WebApplication.CreateBuilder(args);
var settings = builder.Configuration.Get<Settings>()!;
builder.Services.AddChannelConsumer<InstanceEventConsumer, InstanceEvent>(consumers: 1, capacity: 10);
// builder.Services.AddHostedService<InstanceUpdateStreamBackgroundService>();
builder.Services.AddSingleton<InstanceHistoryStreamBackgroundService>();
builder.Services.AddHostedService<InstanceHistoryStreamBackgroundService>(x => x.GetRequiredService<InstanceHistoryStreamBackgroundService>());
builder.Services.AddTransient<InstanceStreamer>();

// Http clients
builder.Services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(
    defaultMaskinportenClientDefinitionKey, 
    settings.Maskinporten);
builder.Services.AddRefitClient<IStorageAdapterApi>()
    .ConfigureHttpClient(x =>
    {
        x.BaseAddress = new Uri("https://localhost:7241");
        x.Timeout = Timeout.InfiniteTimeSpan;
    });
builder.Services.AddHttpClient(Constants.MaskinportenClientDefinitionKey)
    .ConfigureHttpClient(x => x.BaseAddress = new Uri("https://platform.tt02.altinn.no"))
    .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(defaultMaskinportenClientDefinitionKey);
builder.Services.AddTransient<IStorageApi>(x => RestService
    .For<IStorageApi>(x.GetRequiredService<IHttpClientFactory>()
        .CreateClient(Constants.MaskinportenClientDefinitionKey)));


var app = builder.Build();

app.MapGet("/api/v1/instanceHistoryStream/pause",async (
        [FromServices] InstanceHistoryStreamBackgroundService historyStream, 
        CancellationToken cancellationToken) 
    => await historyStream.Pause(cancellationToken));

app.MapGet("/api/v1/instanceHistoryStream/resume",async (
        [FromServices] InstanceHistoryStreamBackgroundService historyStream, 
        CancellationToken cancellationToken) 
    => await historyStream.Resume(cancellationToken));

await app.RunAsync();
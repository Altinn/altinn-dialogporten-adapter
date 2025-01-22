using Altinn.Platform.DialogportenAdapter.EventSimulator;
using Altinn.Platform.DialogportenAdapter.EventSimulator.Infrastructure;
using Cocona;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refit;

const int producers = 1;
const int consumers = 1;
const int cacheSize = 5;

var builder = CoconaApp.CreateBuilder(args);

builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddTransient<EventStreamer>();
builder.Services.AddRefitClient<IStorageApi>()
    .ConfigureHttpClient(x => x.BaseAddress = new Uri("https://platform.tt02.altinn.no"));
builder.Services.AddRefitClient<IAltinnCdnApi>()
    .ConfigureHttpClient(x => x.BaseAddress = new Uri("https://altinncdn.no/"));
builder.Services.AddHttpClient<InstanceStreamHttpClient>()
    .ConfigureHttpClient(x => x.BaseAddress = new Uri("https://platform.tt02.altinn.no"));
builder.Services.AddRefitClient<ITestTokenApi>()
    .ConfigureHttpClient(x =>
    {
        x.BaseAddress = new Uri("https://altinn-testtools-token-generator.azurewebsites.net/");
        x.DefaultRequestHeaders.Add("Authorization", "Basic ZHBvY3Rlc3Q6bWtiRlhsM2h5RWxBTlZwZ2Jha28=");
    });
builder.Services.AddRefitClient<IStorageAdapterApi>()
    .ConfigureHttpClient(x =>
    {
        x.BaseAddress = new Uri("https://localhost:7241");
        x.Timeout = Timeout.InfiniteTimeSpan;
    });

var app = builder.Build();

app.AddCommand(async (CoconaAppContext ctx, EventStreamer eventStreamer) 
    => await eventStreamer.StreamEvents(producers, consumers, cacheSize, ctx.CancellationToken));

await app.RunAsync();
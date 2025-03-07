using Altinn.DialogportenAdapter.EventSimulator;
using Altinn.DialogportenAdapter.EventSimulator.Common;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Refit;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.SetMinimumLevel(LogLevel.Warning);
// builder.Logging.AddFilter("Altinn.DialogportenAdapter.EventSimulator.EventStreamer", LogLevel.Information);

// TODO: Change from DigdirApplicationService to ApplicationService when scope 'altinn:storage/instances.syncadapter' is implemented in storage
// https://digdir.slack.com/archives/C0785747G6M/p1737459622842289
builder.Services.AddChannelConsumer<InstanceEventConsumer, InstanceEvent>(consumers: 1, capacity: 10);
builder.Services.AddTransient<EventStreamer>();
builder.Services.AddRefitClient<IStorageApi>()
    .ConfigureHttpClient(x => x.BaseAddress = new Uri("https://platform.tt02.altinn.no"));
builder.Services.AddRefitClient<IAltinnCdnApi>()
    .ConfigureHttpClient(x => x.BaseAddress = new Uri("https://altinncdn.no/"));
builder.Services.AddHttpClient<InstanceEventStreamer>()
    .ConfigureHttpClient(x => x.BaseAddress = new Uri("https://platform.tt02.altinn.no"));
// builder.Services.AddRefitClient<ITestTokenApi>()
//     .ConfigureHttpClient(x =>
//     {
//         x.BaseAddress = new Uri("https://altinn-testtools-token-generator.azurewebsites.net/");
//         x.DefaultRequestHeaders.Add("Authorization", "Basic ZHBvY3Rlc3Q6bWtiRlhsM2h5RWxBTlZwZ2Jha28=");
//     });
builder.Services.AddRefitClient<IStorageAdapterApi>()
    .ConfigureHttpClient(x =>
    {
        x.BaseAddress = new Uri("https://localhost:7241");
        x.Timeout = Timeout.InfiniteTimeSpan;
    });

var app = builder.Build();

app.MapGet("/api/v1/streamEvents",async (
        [FromServices] EventStreamer eventStreamer, 
        CancellationToken cancellationToken) 
    => await eventStreamer.StreamEvents(numberOfProducers: 2, cancellationToken));

await app.RunAsync();
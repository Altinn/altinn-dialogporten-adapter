using System.Collections.Concurrent;
using System.Threading.Channels;
using Altinn.Platform.DialogportenAdapter.EventSimulator.Infrastructure;

namespace Altinn.Platform.DialogportenAdapter.EventSimulator;

internal sealed class EventStreamer
{
    private readonly IStorageApi _storageApi;
    private readonly IAltinnCdnApi _altinnCdnApi;
    private readonly ITestTokenApi _testTokenApi;
    private readonly InstanceStreamHttpClient _instanceStreamHttpClient;
    private readonly IStorageAdapterApi _storageAdapterApi;

    public EventStreamer(IStorageApi storageApi,
        IAltinnCdnApi altinnCdnApi,
        ITestTokenApi testTokenApi,
        InstanceStreamHttpClient instanceStreamHttpClient,
        IStorageAdapterApi storageAdapterApi)
    {
        _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
        _altinnCdnApi = altinnCdnApi ?? throw new ArgumentNullException(nameof(altinnCdnApi));
        _testTokenApi = testTokenApi ?? throw new ArgumentNullException(nameof(testTokenApi));
        _instanceStreamHttpClient = instanceStreamHttpClient ?? throw new ArgumentNullException(nameof(instanceStreamHttpClient));
        _storageAdapterApi = storageAdapterApi ?? throw new ArgumentNullException(nameof(storageAdapterApi));
    }

    public async Task StreamEvents(CancellationToken cancellationToken)
    {
        const int producers = 1;
        const int consumers = 1;
        const int cacheSize = 5;
        var instanceEventChannel = Channel.CreateBounded<InstanceEvent>(cacheSize);
        var producerTask = ProduceEvents(producers, instanceEventChannel, cancellationToken);
        var consumerTask = ConsumeEvents(consumers, instanceEventChannel, cancellationToken);
        await Task.WhenAll(producerTask, consumerTask);
    }
    
    private async Task ProduceEvents(int producers, ChannelWriter<InstanceEvent> writer, CancellationToken cancellationToken)
    {
        var appQueue = new ConcurrentQueue<AppInfo>(await GetApplicationInfo(cancellationToken));
        await Task.WhenAll(Enumerable.Range(1, producers).Select(ProduceEventsForApp));
        writer.Complete();
        return;

        async Task ProduceEventsForApp(int _)
        {
            while (!cancellationToken.IsCancellationRequested && appQueue.TryDequeue(out var appInfo))
            {
                await foreach (var instance in _instanceStreamHttpClient.GetInstanceStream(appInfo.AppId, appInfo.OrgToken, cancellationToken))
                {
                    var (partyId, instanceId) = instance.Id.Split("/") switch
                    {
                        [var pId, var iId] when int.TryParse(pId, out var x) && Guid.TryParse(iId, out var y) => (x, y),
                        _ => throw new InvalidOperationException("Invalid instance id")
                    };
                    var instanceEvent = new InstanceEvent(instance.AppId, partyId, instanceId, instance.Created);
                    Console.WriteLine($"Producing event for {instanceId}");
                    await writer.WriteAsync(instanceEvent,cancellationToken);
                }
            }
        }
    }
    
    private async Task ConsumeEvents(int consumers, ChannelReader<InstanceEvent> reader, CancellationToken cancellationToken)
    {
        await Task.WhenAll(Enumerable.Range(1, consumers).Select(Consume));
        return;
        
        async Task Consume(int _)
        {
            while (await reader.WaitToReadAsync(cancellationToken))
            {
                while (reader.TryRead(out var instanceEvent))
                {
                    Console.WriteLine($"Consuming event for {instanceEvent.InstanceId}");
                    await _storageAdapterApi.Sync(instanceEvent, cancellationToken);
                }
            }
        }
    }
    
    private async Task<List<AppInfo>> GetApplicationInfo(CancellationToken cancellationToken)
    {
        var orgsResponse = await _altinnCdnApi.GetOrgs(cancellationToken);
        var applications = await _storageApi.GetApplications(cancellationToken);
        
        var orgNoByName = orgsResponse.Orgs
            .ToDictionary(x => x.Key, x => x.Value.Orgnr);
        var distinctApplication = applications.Applications
            // TODO: Remove this when scope 'altinn:storage/instances.syncadapter' is implemented in storage
            // https://digdir.slack.com/archives/C0785747G6M/p1737459622842289
            .Where(x => StringComparer.OrdinalIgnoreCase.Equals(x.Org, "digdir"))
            .Distinct()
            .ToList();
        
        var tokenByOrg = (await Task.WhenAll(distinctApplication
                .Select(x => x.Org)
                .Distinct()
                .Select(async org =>
                {
                    if (!orgNoByName.TryGetValue(org == "ttd" ? "digdir" : org, out var orgNo))
                    {
                        return (org, token: null!);
                    }
                    var token = await _testTokenApi.GetToken(org, orgNo, cancellationToken);
                    return (org, token);
                })
            ))
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            .Where(x => x.token is not null)
            .ToDictionary(x => x.org, x => x.token);
        
        return distinctApplication
            .Where(x => tokenByOrg.ContainsKey(x.Org))
            .Select(x => new AppInfo(x.Id, tokenByOrg[x.Org]))
            .ToList();
    }

    private record AppInfo(string AppId, string OrgToken);
}

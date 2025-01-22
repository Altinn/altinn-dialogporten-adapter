using System.Collections.Concurrent;
using System.Threading.Channels;
using Altinn.Platform.DialogportenAdapter.EventSimulator.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.DialogportenAdapter.EventSimulator;

internal sealed class EventStreamer
{
    private readonly IStorageApi _storageApi;
    private readonly IAltinnCdnApi _altinnCdnApi;
    private readonly ITestTokenApi _testTokenApi;
    private readonly InstanceStreamHttpClient _instanceStreamHttpClient;
    private readonly IStorageAdapterApi _storageAdapterApi;
    private readonly ILogger<EventStreamer> _logger;

    public EventStreamer(IStorageApi storageApi,
        IAltinnCdnApi altinnCdnApi,
        ITestTokenApi testTokenApi,
        InstanceStreamHttpClient instanceStreamHttpClient,
        IStorageAdapterApi storageAdapterApi, 
        ILogger<EventStreamer> logger)
    {
        _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
        _altinnCdnApi = altinnCdnApi ?? throw new ArgumentNullException(nameof(altinnCdnApi));
        _testTokenApi = testTokenApi ?? throw new ArgumentNullException(nameof(testTokenApi));
        _instanceStreamHttpClient = instanceStreamHttpClient ?? throw new ArgumentNullException(nameof(instanceStreamHttpClient));
        _storageAdapterApi = storageAdapterApi ?? throw new ArgumentNullException(nameof(storageAdapterApi));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StreamEvents(
        int numberOfProducers, 
        int numberOfConsumers, 
        int cacheCapacity, 
        CancellationToken cancellationToken)
    {
        var instanceEventChannel = Channel.CreateBounded<InstanceEvent>(cacheCapacity);
        var producerTask = ProduceEvents(numberOfProducers, instanceEventChannel, cancellationToken);
        var consumerTask = ConsumeEvents(numberOfConsumers, instanceEventChannel, cancellationToken);
        await Task.WhenAll(producerTask, consumerTask);
    }
    
    private async Task ProduceEvents(int producers, ChannelWriter<InstanceEvent> writer, CancellationToken cancellationToken)
    {
        var appQueue = new ConcurrentQueue<AppInfo>(await GetApplicationInfo(cancellationToken));
        await Task.WhenAll(Enumerable.Range(1, producers).Select(Produce));
        writer.Complete();
        return;

        async Task Produce(int _)
        {
            while (appQueue.TryDequeue(out var appInfo))
            {
                await foreach (var instanceEvent in _instanceStreamHttpClient.GetInstanceStream(appInfo.AppId, appInfo.OrgToken, cancellationToken))
                {
                    _logger.LogInformation("Producing event for {instanceId}", instanceEvent.InstanceId);
                    await writer.WriteAsync(instanceEvent, cancellationToken);
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
            await foreach (var instanceEvent in reader.ReadAllAsync(cancellationToken))
            {
                _logger.LogInformation("Consuming event for {instanceId}", instanceEvent.InstanceId);
                await _storageAdapterApi.Sync(instanceEvent, cancellationToken);
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

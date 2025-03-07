using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

namespace Altinn.DialogportenAdapter.EventSimulator.Common;

internal sealed class FetchNewestInstanceBackgroundService : BackgroundService
{
    private readonly IChannelPublisher<InstanceEvent> _channelPublisher;
    private readonly InstanceEventStreamer _instanceEventStreamer;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    
    public FetchNewestInstanceBackgroundService(
        IChannelPublisher<InstanceEvent> channelPublisher,
        InstanceEventStreamer instanceEventStreamer,
        IServiceScopeFactory serviceScopeFactory)
    {
        _channelPublisher = channelPublisher ?? throw new ArgumentNullException(nameof(channelPublisher));
        _instanceEventStreamer = instanceEventStreamer ?? throw new ArgumentNullException(nameof(instanceEventStreamer));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
    }
    
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var orgs = await GetDistinctStorageOrgs(cancellationToken);
        var initialFetch = DateTimeOffset.UtcNow.AddMinutes(-10);
        await Task.WhenAll(orgs.Select(org => Produce(org, initialFetch, cancellationToken)));
    }

    private async Task Produce(string org, DateTimeOffset initialFetch, CancellationToken cancellationToken)
    {
        await foreach (var instanceEvent in _instanceEventStreamer.InstanceUpdateStream(
            org,
            from: initialFetch,
            pauseDuration: TimeSpan.FromSeconds(10),
            cancellationToken))
        {
            await _channelPublisher.Publish(instanceEvent, cancellationToken);
        }
    }

    private async Task<List<string>> GetDistinctStorageOrgs(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var apps = await scope.ServiceProvider
            .GetRequiredService<IStorageApi>()
            .GetApplications(cancellationToken);
        return apps.Applications
            .Select(x => x.Org)
            .Distinct()
            .ToList();
    }
}
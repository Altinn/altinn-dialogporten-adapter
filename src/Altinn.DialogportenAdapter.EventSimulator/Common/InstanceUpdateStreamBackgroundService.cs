using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

namespace Altinn.DialogportenAdapter.EventSimulator.Common;

internal sealed class InstanceUpdateStreamBackgroundService : BackgroundService
{
    private readonly IChannelPublisher<InstanceEvent> _channelPublisher;
    private readonly InstanceEventStreamer _instanceEventStreamer;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<InstanceEventConsumer> _logger;
    
    public InstanceUpdateStreamBackgroundService(
        IChannelPublisher<InstanceEvent> channelPublisher,
        InstanceEventStreamer instanceEventStreamer,
        IServiceScopeFactory serviceScopeFactory, 
        ILogger<InstanceEventConsumer> logger)
    {
        _channelPublisher = channelPublisher ?? throw new ArgumentNullException(nameof(channelPublisher));
        _instanceEventStreamer = instanceEventStreamer ?? throw new ArgumentNullException(nameof(instanceEventStreamer));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var orgs = await GetDistinctStorageOrgs(cancellationToken);
        _logger.LogInformation("Found {OrgCount} orgs.}", orgs.Count);
        var from = DateTimeOffset.UtcNow.AddMinutes(-10);
        await Task.WhenAll(orgs.Select(org => Produce(org, from, cancellationToken)));
    }

    private async Task Produce(string org, DateTimeOffset from, CancellationToken cancellationToken)
    {
        await foreach (var instanceEvent in _instanceEventStreamer.InstanceUpdateStream(
            org,
            from: from,
            cancellationToken))
        {
            await _channelPublisher.Publish(instanceEvent.ToInstanceEvent(isMigration: false), cancellationToken);
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
using Altinn.DialogportenAdapter.EventSimulator.Common;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

namespace Altinn.DialogportenAdapter.EventSimulator.Features;

internal sealed class InstanceUpdateStreamBackgroundService : BackgroundService
{
    private readonly IChannelPublisher<InstanceEvent> _channelPublisher;
    private readonly InstanceEventStreamer _instanceEventStreamer;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<InstanceEventConsumer> _logger;
    private List<string>? _orgs;

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

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _orgs = await GetDistinctStorageOrgs(cancellationToken);
        _logger.LogInformation("Found {OrgCount} orgs.", _orgs.Count);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (_orgs is null || _orgs.Count == 0)
        {
            throw new InvalidOperationException("No orgs were found.");
        }
        
        var from = DateTimeOffset.UtcNow.AddMinutes(-10);
        await Task.WhenAll(_orgs.Select(org => Produce(org, from, cancellationToken)));
    }

    private async Task Produce(string org, DateTimeOffset from, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var instanceDto in _instanceEventStreamer.InstanceUpdateStream(
                                   org,
                                   from: from,
                                   cancellationToken))
                {
                    await _channelPublisher.Publish(instanceDto.ToInstanceEvent(isMigration: false), cancellationToken);
                    from = instanceDto.LastChanged > from ? instanceDto.LastChanged : from;
                }
            }
            catch (Exception e) when (e is TaskCanceledException or OperationCanceledException) { /* Swallow by design */ }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while consuming instance update stream. Attempting to reset stream in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
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
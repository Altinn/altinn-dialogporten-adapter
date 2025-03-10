using Altinn.DialogportenAdapter.EventSimulator.Common;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

namespace Altinn.DialogportenAdapter.EventSimulator.Features;

internal sealed class InstanceHistoryStreamBackgroundService : BackgroundService
{
    private readonly PauseContext<InstanceHistoryStreamBackgroundService> _pauseContext;
    private readonly IChannelPublisher<InstanceEvent> _channelPublisher;
    private readonly InstanceStreamer _instanceStreamer;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<InstanceEventConsumer> _logger;
    private List<string>? _orgs;

    public InstanceHistoryStreamBackgroundService(
        IChannelPublisher<InstanceEvent> channelPublisher,
        InstanceStreamer instanceStreamer,
        IServiceScopeFactory serviceScopeFactory, 
        ILogger<InstanceEventConsumer> logger, 
        PauseContext<InstanceHistoryStreamBackgroundService> pauseContext)
    {
        _channelPublisher = channelPublisher ?? throw new ArgumentNullException(nameof(channelPublisher));
        _instanceStreamer = instanceStreamer ?? throw new ArgumentNullException(nameof(instanceStreamer));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pauseContext = pauseContext;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _orgs = await GetDistinctStorageOrgs(cancellationToken);
        _logger.LogInformation("Found {OrgCount} orgs.", _orgs.Count);
        await base.StartAsync(cancellationToken);
    }

    public Task Pause(CancellationToken cancellationToken) => _pauseContext.Pause(cancellationToken);

    public Task Resume(CancellationToken cancellationToken) => _pauseContext.Resume(cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (_orgs is null || _orgs.Count == 0)
        {
            throw new InvalidOperationException("No orgs were found.");
        }
        
        var to = DateTimeOffset.UtcNow;
        await Task.WhenAll(_orgs.Select(org => Produce(org, to, cancellationToken)));
    }

    private async Task Produce(string org, DateTimeOffset to, CancellationToken cancellationToken)
    {
        using var pauseHandler = await _pauseContext.CreatePauseHandler(org, cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var instanceDto in _instanceStreamer.InstanceHistoryStream(
                                   org: org,
                                   to: to,
                                   cancellationToken))
                {
                    await pauseHandler.AwaitPause(cancellationToken); 
                    await _channelPublisher.Publish(instanceDto.ToInstanceEvent(isMigration: false), cancellationToken);
                    to = instanceDto.LastChanged < to ? instanceDto.LastChanged : to;
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
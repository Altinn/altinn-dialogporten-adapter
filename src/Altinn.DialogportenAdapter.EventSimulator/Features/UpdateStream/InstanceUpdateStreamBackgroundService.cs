using Altinn.DialogportenAdapter.EventSimulator.Common.Channels;
using Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;
using Altinn.DialogportenAdapter.EventSimulator.Features.InstanceEventForwarder;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;
using Altinn.Storage.Contracts;

namespace Altinn.DialogportenAdapter.EventSimulator.Features.UpdateStream;

internal sealed class InstanceUpdateStreamBackgroundService : BackgroundService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IChannelPublisher<InstanceUpdatedEvent> _channelPublisher;
    private readonly InstanceStreamer _instanceStreamer;
    private readonly ILogger<InstanceEventToAdapterThroughHttp> _logger;
    private readonly Settings _settings;

    public InstanceUpdateStreamBackgroundService(
        IChannelPublisher<InstanceUpdatedEvent> channelPublisher,
        InstanceStreamer instanceStreamer,
        ILogger<InstanceEventToAdapterThroughHttp> logger,
        IOrganizationRepository organizationRepository,
        Settings settings)
    {
        _channelPublisher = channelPublisher ?? throw new ArgumentNullException(nameof(channelPublisher));
        _instanceStreamer = instanceStreamer ?? throw new ArgumentNullException(nameof(instanceStreamer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!_settings.DialogportenAdapter.EventSimulator.EnableUpdateStream)
        {
            _logger.LogDebug("Update stream processing is disabled.");
            return;
        }

        var orgs = await _organizationRepository.GetOrganizations(cancellationToken);
        _logger.LogInformation("Found {OrgCount} orgs.", orgs.Count);
        if (orgs is null || orgs.Count == 0)
        {
            throw new InvalidOperationException("No orgs were found.");
        }

        var from = DateTimeOffset.UtcNow.AddMinutes(-10);
        await Task.WhenAll(orgs.Select(org => Produce(org, from, cancellationToken)));
    }

    private async Task Produce(string org, DateTimeOffset from, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var instanceDto in _instanceStreamer.InstanceUpdateStream(
                                   org,
                                   from: from,
                                   cancellationToken))
                {
                    await _channelPublisher.Publish(instanceDto.ToInstanceEvent(isMigration: false), cancellationToken);
                    from = instanceDto.LastChanged > from ? instanceDto.LastChanged : from;
                }
            }
            catch (OperationCanceledException) { /* Swallow by design */ }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while consuming instance update stream for org {org}. Attempting to reset stream in 5 seconds.", org);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }
}
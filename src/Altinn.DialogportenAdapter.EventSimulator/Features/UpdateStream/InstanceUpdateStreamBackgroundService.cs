using Altinn.DialogportenAdapter.EventSimulator.Common.Channels;
using Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

namespace Altinn.DialogportenAdapter.EventSimulator.Features.UpdateStream;

internal sealed class InstanceUpdateStreamBackgroundService : BackgroundService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IChannelPublisher<InstanceEvent> _channelPublisher;
    private readonly InstanceStreamer _instanceStreamer;
    private readonly ILogger<InstanceEventConsumer> _logger;

    public InstanceUpdateStreamBackgroundService(
        IChannelPublisher<InstanceEvent> channelPublisher,
        InstanceStreamer instanceStreamer,
        ILogger<InstanceEventConsumer> logger,
        IOrganizationRepository organizationRepository)
    {
        _channelPublisher = channelPublisher ?? throw new ArgumentNullException(nameof(channelPublisher));
        _instanceStreamer = instanceStreamer ?? throw new ArgumentNullException(nameof(instanceStreamer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
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
            catch (Exception e) when (e is TaskCanceledException or OperationCanceledException) { /* Swallow by design */ }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while consuming instance update stream for org {org}. Attempting to reset stream in 5 seconds.", org);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }
}
using Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;
using Wolverine;

namespace Altinn.DialogportenAdapter.EventSimulator.Features.UpdateStream;

internal sealed class InstanceUpdateStreamBackgroundService : BackgroundService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IInstanceStreamer _instanceStreamer;
    private readonly ILogger<InstanceUpdateStreamBackgroundService> _logger;
    private readonly Settings _settings;
    private readonly IServiceProvider _serviceProvider;

    public InstanceUpdateStreamBackgroundService(
        IInstanceStreamer instanceStreamer,
        ILogger<InstanceUpdateStreamBackgroundService> logger,
        IOrganizationRepository organizationRepository,
        Settings settings,
        IServiceProvider serviceProvider)
    {
        _instanceStreamer = instanceStreamer ?? throw new ArgumentNullException(nameof(instanceStreamer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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
                    using var scope = _serviceProvider.CreateScope();
                    var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                    await bus.SendAsync(instanceDto.ToSyncInstanceCommand(isMigration: false));
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
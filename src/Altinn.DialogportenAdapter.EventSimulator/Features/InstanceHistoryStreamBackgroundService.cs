using System.Collections.Concurrent;
using Altinn.DialogportenAdapter.EventSimulator.Common;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

namespace Altinn.DialogportenAdapter.EventSimulator.Features;

internal sealed class InstanceHistoryStreamBackgroundService : BackgroundService
{
    private readonly IChannelPublisher<InstanceEvent> _channelPublisher;
    private readonly InstanceStreamer _instanceStreamer;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<InstanceEventConsumer> _logger;

    private readonly SemaphoreSlim _semaphoresByOrgLock = new(1, 1);
    private readonly ConcurrentDictionary<string, WeakReference<SemaphoreSlim>> _semaphoreByOrg = [];
    private List<string>? _orgs;
    private bool _paused = true;

    public InstanceHistoryStreamBackgroundService(
        IChannelPublisher<InstanceEvent> channelPublisher,
        InstanceStreamer instanceStreamer,
        IServiceScopeFactory serviceScopeFactory, 
        ILogger<InstanceEventConsumer> logger)
    {
        _channelPublisher = channelPublisher ?? throw new ArgumentNullException(nameof(channelPublisher));
        _instanceStreamer = instanceStreamer ?? throw new ArgumentNullException(nameof(instanceStreamer));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _orgs = await GetDistinctStorageOrgs(cancellationToken);
        _logger.LogInformation("Found {OrgCount} orgs.", _orgs.Count);
        await base.StartAsync(cancellationToken);
    }
    
    public async Task Pause(CancellationToken cancellationToken)
    {
        await _semaphoresByOrgLock.WaitAsync(cancellationToken);
        try
        {
            await Task.WhenAll(GetStrongSemaphores().Select(x => x.WaitAsync(cancellationToken)));
            _paused = true;
        }
        finally
        {
            _semaphoresByOrgLock.Release();
        }
    }

    public async Task Resume(CancellationToken cancellationToken)
    {
        await _semaphoresByOrgLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var semaphore in GetStrongSemaphores().Where(x => x.CurrentCount == 0))
            {
                semaphore.Release();
            }
            
            _paused = false;
        }
        finally
        {
            _semaphoresByOrgLock.Release();
        }
    }

    private async Task<SemaphoreSlim> CreateSemaphore(string org, CancellationToken cancellationToken)
    {
        await _semaphoresByOrgLock.WaitAsync(cancellationToken);
        try
        {
            var newSemaphore = new SemaphoreSlim(_paused ? 0 : 1, 1);
            var weakExistingSemaphore = _semaphoreByOrg.GetOrAdd(org, new WeakReference<SemaphoreSlim>(newSemaphore));
            if (!weakExistingSemaphore.TryGetTarget(out var existingSemaphore))
            {
                weakExistingSemaphore.SetTarget(existingSemaphore = newSemaphore);
            }

            if (newSemaphore != existingSemaphore)
            {
                newSemaphore.Dispose();
            }
            
            return existingSemaphore;
        }
        finally
        {
            _semaphoresByOrgLock.Release();
        }
    }

    private IEnumerable<SemaphoreSlim> GetStrongSemaphores()
    {
        foreach (var weakSemaphore in _semaphoreByOrg.Values)
        {
            if (weakSemaphore.TryGetTarget(out var semaphore))
            {
                yield return semaphore;
            }
        }
    }

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
        using var semaphore = await CreateSemaphore(org, cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var instanceDto in _instanceStreamer.InstanceHistoryStream(
                                   org: org,
                                   to: to,
                                   cancellationToken))
                {
                    await semaphore.WaitAsync(cancellationToken); // Wait if paused
                    semaphore.Release(); // Ensure it remains available for next iteration
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

internal class PauseContext
{
    private readonly SemaphoreSlim _semaphoresByOrgLock = new(1, 1);
    private readonly ConcurrentDictionary<string, WeakReference<SemaphoreSlim>> _semaphoreByKey = [];
    public bool Paused { get; private set; }

    public async Task Pause(CancellationToken cancellationToken)
    {
        await _semaphoresByOrgLock.WaitAsync(cancellationToken);
        try
        {
            await Task.WhenAll(GetStrongSemaphores().Select(x => x.WaitAsync(cancellationToken)));
            Paused = true;
        }
        finally
        {
            _semaphoresByOrgLock.Release();
        }
    }

    public async Task Resume(CancellationToken cancellationToken)
    {
        await _semaphoresByOrgLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var semaphore in GetStrongSemaphores().Where(x => x.CurrentCount == 0))
            {
                semaphore.Release();
            }
            
            Paused = false;
        }
        finally
        {
            _semaphoresByOrgLock.Release();
        }
    }

    public async Task<Handler> CreatePauseHandler(string key, CancellationToken cancellationToken)
    {
        await _semaphoresByOrgLock.WaitAsync(cancellationToken);
        try
        {
            var newSemaphore = new SemaphoreSlim(Paused ? 0 : 1, 1);
            var weakExistingSemaphore = _semaphoreByKey.GetOrAdd(key, new WeakReference<SemaphoreSlim>(newSemaphore));
            if (!weakExistingSemaphore.TryGetTarget(out var existingSemaphore))
            {
                weakExistingSemaphore.SetTarget(existingSemaphore = newSemaphore);
            }

            if (newSemaphore != existingSemaphore)
            {
                newSemaphore.Dispose();
            }

            return new Handler(existingSemaphore);
        }
        finally
        {
            _semaphoresByOrgLock.Release();
        }
    }

    private IEnumerable<SemaphoreSlim> GetStrongSemaphores()
    {
        foreach (var weakSemaphore in _semaphoreByKey.Values)
        {
            if (weakSemaphore.TryGetTarget(out var semaphore))
            {
                yield return semaphore;
            }
        }
    }
    
    internal class Handler : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public Handler(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public async Task WaitForPause(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            _semaphore.Release();
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}

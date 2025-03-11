using System.Collections.Concurrent;

namespace Altinn.DialogportenAdapter.EventSimulator.Common;

internal class PauseContext<T>
{
    private readonly SemaphoreSlim _semaphoresByOrgLock = new(1, 1);
    private readonly ConcurrentDictionary<string, WeakReference<SemaphoreSlim>> _semaphoreByKey = [];
    public bool Paused { get; private set; }

    public PauseContext(bool paused = true)
    {
        Paused = paused;
    }

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

        public async Task AwaitPause(CancellationToken cancellationToken)
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
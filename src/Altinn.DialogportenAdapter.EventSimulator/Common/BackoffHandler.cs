namespace Altinn.DialogportenAdapter.EventSimulator.Common;

internal sealed class BackoffHandler
{
    public const double JitterPercentage = 10;
    private readonly bool _withJitter;
    private readonly List<TimeSpan> _delays;
    private int _currentIndex;

    public BackoffHandler(bool withJitter, Position startPosition, params IEnumerable<TimeSpan> delays)
    {
        _withJitter = withJitter;
        _delays = delays.ToList();
        if (_delays.Count == 0)
        {
            throw new ArgumentException("Delays list cannot be empty.");
        }

        _currentIndex = startPosition switch
        {
            Position.First => 0,
            Position.Last => _delays.Count - 1,
            _ => throw new ArgumentException($"Invalid backoff position: {startPosition}")
        };
    }

    public TimeSpan Current => _delays[_currentIndex];

    public void Next()
    {
        if (_currentIndex < _delays.Count - 1)
            _currentIndex++;
    }

    public void Reset()
    {
        _currentIndex = 0;
    }
    
    public Task Delay(CancellationToken cancellationToken)
    {
        return Task.Delay(GetDelayTimeSpan(), cancellationToken);
    }

    private TimeSpan GetDelayTimeSpan()
    {
        return _withJitter ? GetJitter() + Current : Current;
    }

    private TimeSpan GetJitter()
    {
        var jitterRange = (Current * (JitterPercentage / 100)).Ticks;
        var randomJitter = Random.Shared.NextInt64(-jitterRange, jitterRange);
        return TimeSpan.FromTicks(randomJitter);
    }
    
    internal enum Position { First, Last }
}
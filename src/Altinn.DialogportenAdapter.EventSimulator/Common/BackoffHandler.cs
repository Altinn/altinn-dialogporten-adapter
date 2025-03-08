namespace Altinn.DialogportenAdapter.EventSimulator.Common;

internal sealed class BackoffHandler
{
    private readonly List<TimeSpan> _delays;
    private int _currentIndex;

    public BackoffHandler(IEnumerable<TimeSpan> delays)
    {
        _delays = delays.ToList();
        if (_delays.Count == 0)
            throw new ArgumentException("Delays list cannot be empty.");

        _currentIndex = 0;
    }

    public TimeSpan Current => _delays[_currentIndex];

    public void Next()
    {
        if (_currentIndex < _delays.Count - 1)
            _currentIndex++;
    }

    public void Previous()
    {
        if (_currentIndex > 0)
            _currentIndex--;
    }

    public void Reset()
    {
        _currentIndex = 0;
    }
}
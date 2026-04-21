namespace Altinn.DialogportenAdapter.WebApi.Common;

public interface IClock
{
    public TimeSpan Seconds(int seconds);
    public TimeSpan Minutes(int minutes);
}

internal sealed class Clock : IClock
{
    public TimeSpan Seconds(int seconds) => TimeSpan.FromSeconds(seconds);
    public TimeSpan Minutes(int minutes) => TimeSpan.FromMinutes(minutes);
}

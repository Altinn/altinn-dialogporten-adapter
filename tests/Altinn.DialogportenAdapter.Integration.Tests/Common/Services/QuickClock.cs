using Altinn.DialogportenAdapter.WebApi.Common;

namespace Altinn.DialogportenAdapter.Integration.Tests.Common.Services;

public class QuickClock : IClock
{
    public TimeSpan Seconds(int seconds) => TimeSpan.FromMilliseconds(1);
    public TimeSpan Minutes(int minutes) => TimeSpan.FromMilliseconds(10);
}

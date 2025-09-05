namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

public static class IntExtensions
{
    // Gives an int approximation, ensure that we always get at least 1 and round up.
    public static int PercentOf(this int value, int percent)
        => Math.Max((int)Math.Ceiling((decimal)value * percent / 100), 1);
}
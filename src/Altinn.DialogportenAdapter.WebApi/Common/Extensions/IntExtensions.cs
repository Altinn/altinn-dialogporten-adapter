namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

public static class IntExtensions
{
    public static int PercentOf(this int value, int percent) => value * percent / 100;
}
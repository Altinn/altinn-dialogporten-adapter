using Microsoft.AspNetCore.Authorization;

namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

internal static class AuthorizationExtensions
{
    public const string ScopeClaim = "scope";
    private const char ScopeClaimSeparator = ' ';

    public static AuthorizationPolicyBuilder RequireScope(this AuthorizationPolicyBuilder builder, string scope) =>
        builder.RequireAssertion(ctx => ctx.User.Claims
            .Where(x => x.Type == ScopeClaim)
            .Select(x => x.Value)
            .Any(scopeValue => scopeValue.AsSpan().SplitContains(ScopeClaimSeparator, scope)));

    private static bool SplitContains(this ReadOnlySpan<char> span, char separator, ReadOnlySpan<char> value)
    {
        var enumerator = span.Split(separator);
        while (enumerator.MoveNext())
        {
            if (span[enumerator.Current].Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
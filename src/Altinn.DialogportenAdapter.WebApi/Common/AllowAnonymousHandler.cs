using Microsoft.AspNetCore.Authorization;

namespace Altinn.DialogportenAdapter.WebApi.Common;

/// <summary>
/// This authorization handler will bypass all requirements
/// </summary>
internal sealed class AllowAnonymousHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        foreach (var requirement in context.PendingRequirements)
        {
            //Simply pass all requirements
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
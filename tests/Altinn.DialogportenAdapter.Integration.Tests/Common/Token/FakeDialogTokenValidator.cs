using System.Security.Claims;
using Altinn.ApiClients.Dialogporten;

namespace Altinn.DialogportenAdapter.Integration.Tests.Common.Token;

public sealed class FakeDialogTokenValidator : IDialogTokenValidator
{
    public IValidationResult Validate(
        ReadOnlySpan<char> token,
        Guid? dialogId = null, string[]? requiredActions = null,
        DialogTokenValidationParameters? options = null
    )
    {
        return new ValidationResult();
    }
}

public sealed class ValidationResult : IValidationResult
{
    public bool IsValid { get; } = true;
    public Dictionary<string, List<string>> Errors { get; } = new();
    public ClaimsPrincipal? ClaimsPrincipal { get; }
}

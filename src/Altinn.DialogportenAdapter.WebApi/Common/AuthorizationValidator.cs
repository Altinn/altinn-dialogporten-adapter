using Altinn.ApiClients.Dialogporten;

namespace Altinn.DialogportenAdapter.WebApi.Common;

internal sealed class AuthorizationValidator(IDialogTokenValidator dialogTokenValidator)
{
    private readonly IDialogTokenValidator _dialogTokenValidator = dialogTokenValidator ?? throw new ArgumentNullException(nameof(dialogTokenValidator));

    public bool ValidateDialogToken(ReadOnlySpan<char> token, Guid dialogId)
    {
        const string bearerPrefix = "Bearer ";
        token = token.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? token[bearerPrefix.Length..]
            : token;
        var result = _dialogTokenValidator.Validate(token, dialogId);
        return result.IsValid;
    }
}

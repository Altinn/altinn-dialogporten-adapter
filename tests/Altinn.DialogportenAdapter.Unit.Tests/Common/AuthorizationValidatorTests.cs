using System.Security.Claims;
using Altinn.ApiClients.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Common;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common;

public class AuthorizationValidatorTests
{
    [Fact]
    public void Ctor_NullDialogTokenValidator_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new AuthorizationValidator(null!));
        Assert.Equal("dialogTokenValidator", exception.ParamName);
    }

    [Fact]
    public void ValidateDialogToken_WithBearerPrefix_StripsPrefixAndForwardsArguments()
    {
        var dialogId = Guid.NewGuid();
        var actions = new[] { "read" };
        var inner = new CapturingDialogTokenValidator(new ValidationResultStub(true));
        var sut = new AuthorizationValidator(inner);
        var result = sut.ValidateDialogToken("Bearer my-token".AsSpan(), dialogId, actions);

        Assert.True(result);
        Assert.Equal("my-token", inner.CapturedToken);
        Assert.Equal(dialogId, inner.CapturedDialogId);
        Assert.Equal(actions, inner.CapturedActions);
    }

    [Fact]
    public void ValidateDialogToken_WithoutPrefix_PassesTokenAsIs()
    {
        var dialogId = Guid.NewGuid();
        var actions = new[] { "delete" };
        var inner = new CapturingDialogTokenValidator(new ValidationResultStub(false));
        var sut = new AuthorizationValidator(inner);

        var result = sut.ValidateDialogToken("raw-token".AsSpan(), dialogId, actions);

        Assert.False(result);
        Assert.Equal("raw-token", inner.CapturedToken);
        Assert.Equal(dialogId, inner.CapturedDialogId);
        Assert.Equal(actions, inner.CapturedActions);
    }

    private sealed class CapturingDialogTokenValidator(IValidationResult result) : IDialogTokenValidator
    {
        public string? CapturedToken { get; private set; }
        public Guid? CapturedDialogId { get; private set; }
        public string[] CapturedActions { get; private set; } = [];

        public IValidationResult Validate(
            ReadOnlySpan<char> token,
            Guid? dialogId = null,
            string[]? requiredActions = null,
            DialogTokenValidationParameters? options = null)
        {
            CapturedToken = token.ToString();
            CapturedDialogId = dialogId;
            CapturedActions = requiredActions ?? [];
            return result;
        }
    }

    private sealed class ValidationResultStub(bool isValid) : IValidationResult
    {
        public bool IsValid { get; } = isValid;
        public Dictionary<string, List<string>> Errors { get; } = [];
        public ClaimsPrincipal? ClaimsPrincipal { get; } = null!;
    }
}

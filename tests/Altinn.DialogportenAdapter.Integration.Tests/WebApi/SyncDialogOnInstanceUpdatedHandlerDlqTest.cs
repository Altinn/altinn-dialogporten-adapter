using System.Net;
using System.Text.Json;
using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.Integration.Tests.Common;
using Altinn.DialogportenAdapter.Integration.Tests.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Altinn.DialogportenAdapter.Integration.Tests.WebApi;

[Collection(nameof(AdapterCollectionFixture))]
public class SyncDialogOnInstanceUpdatedHandlerDlqTest(DialogportenAdapterApplication app)
    : BaseAdapterIntegrationTest(app)
{
    private readonly DialogportenAdapterApplication _app = app;

    [Fact]
    public async Task GivenPostDialogReturnsConflictThenDlqAfter3Retries()
    {
        // Arrange
        var arrangement = ArrangeDefaults();

        _app.DialogportenApi
            .Given(Request.Create().DpPostDialog())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.Conflict));

        // Act
        var result = await SendAndWait(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        var requests = _app.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;

        result.IsSuccess.Should().BeFalse();
        result.DlqMessage.Should().NotBeNull();
        result.DlqMessage.DeadLetterErrorDescription.Should().Contain("Conflict");
        requests.Should().Be(4); // 3 retries + 1 requests
    }

    [Fact]
    public async Task GivenGetDialogReturnsPreconditionFailedThenDlqAfter3Retries()
    {
        // Arrange
        var arrangement = ArrangeDefaults();
        var dialogId = arrangement.DialogId;

        _app.DialogportenApi
            .Given(Request.Create().DpGetDialog(dialogId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.PreconditionFailed));

        // Act
        var result = await SendAndWait(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        var getRequests = _app.DialogportenApi.FindLogEntries(Request.Create().DpGetDialog(dialogId)).Count;
        var postRequests = _app.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;

        result.IsSuccess.Should().BeFalse();
        result.DlqMessage.Should().NotBeNull();
        result.DlqMessage.DeadLetterErrorDescription.Should().Contain("Precondition Failed");
        getRequests.Should().Be(4); // 3 retries + 1 requests
        postRequests.Should().Be(0);
    }

    [Fact]
    public async Task GivenPostDialogReturnsUnprocessableEntityThenDlqAfter9Retries()
    {
        // Arrange
        var arrangement = ArrangeDefaults();

        _app.DialogportenApi
            .Given(Request.Create().DpPostDialog())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.UnprocessableEntity));

        // Act
        var result = await SendAndWait(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        var requests = _app.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;

        result.DlqMessage.Should().NotBeNull();
        result.DlqMessage.DeadLetterErrorDescription.Should().Contain("Unprocessable Entity");
        requests.Should().Be(10); // 9 retries + 1 requests
    }

    [Fact]
    public async Task GivenNoPartiesFoundInRegisterThenDlqAfter6Retries()
    {
        // Arrange
        await _app.AppScope.ServiceProvider
            .GetRequiredService<IFusionCache>()
            .ClearAsync(token: TestContext.Current.CancellationToken);

        var arrangement = ArrangeDefaults();

        _app.RegisterApi
            .Given(Request.Create().RegisterPostPartySearch())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(new PartyQueryResponse([]))));

        // Act
        var result = await SendAndWait(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        var searchRequests = _app.RegisterApi.FindLogEntries(Request.Create().RegisterPostPartySearch()).Count;
        var postRequests = _app.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;

        result.DlqMessage.Should().NotBeNull();
        result.DlqMessage.DeadLetterErrorDescription.Should().Contain("Party with id 2 not found");
        searchRequests.Should().Be(14); // (6 retries + 1 requests) * 2 events
        postRequests.Should().Be(0);
    }

    [Fact]
    public async Task GivenUnauthorizedThenDlqAfter3Retries()
    {
        // Arrange
        var arrangement = ArrangeDefaults();
        var dialogId = arrangement.DialogId;

        _app.DialogportenApi
            .Given(Request.Create().DpGetDialog(dialogId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.Unauthorized));

        // Act
        var result = await SendAndWait(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        var getRequests = _app.DialogportenApi.FindLogEntries(Request.Create().DpGetDialog(dialogId)).Count;
        var postRequests = _app.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;

        result.IsSuccess.Should().BeFalse();
        result.DlqMessage.Should().NotBeNull();
        result.DlqMessage.DeadLetterErrorDescription.Should().Contain("Unauthorized");
        getRequests.Should().Be(8); // (3 retries + 1 requests) * 2 extra request after token refresh attempt
        postRequests.Should().Be(0);
    }

    [Fact]
    public async Task GivenGetDialogDownRetryIndefinitely()
    {
        // Arrange
        var arrangement = ArrangeDefaults();
        var dialogId = arrangement.DialogId;

        _app.DialogportenApi
            .Given(Request.Create().DpGetDialog(dialogId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.ServiceUnavailable));

        // Act
        var result = await SendAndWait(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.DlqMessage.Should().BeNull();

        var getDialogRequests = _app.DialogportenApi.FindLogEntries(Request.Create().DpGetDialog(dialogId)).Count;
        var postDialogLogsRequests = _app.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;

        getDialogRequests.Should().BeGreaterThan(5);
        postDialogLogsRequests.Should().Be(0);
    }
}

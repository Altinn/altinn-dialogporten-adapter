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
public class SyncDialogOnInstanceUpdatedHandlerTest(DialogportenAdapterApplication application)
    : BaseAdapterIntegrationTest(application)
{
    private readonly DialogportenAdapterApplication _application = application;

    [Fact]
    public async Task GivenAllApisWorkProperlyThenDialogIsSaved()
    {
        // Arrange
        var arrangement = ArrangeDefaults();

        // Act
        await Send(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        var dialog = await WaitForDialogPostedOrFail();
        var requests = _application.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;
        dialog.Should().NotBeNull();
        requests.Should().Be(1);
    }

    [Fact]
    public async Task GivenPostDialogReturnsConflictThenDlqAfter3Retries()
    {
        // Arrange
        var arrangement = ArrangeDefaults();

        _application.DialogportenApi
            .Given(Request.Create().DpPostDialog())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.Conflict));

        // Act
        await Send(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        var dlqMessage = await WaitForDlqMessage();
        var requests = _application.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;

        dlqMessage.Should().NotBeNull();
        dlqMessage.DeadLetterErrorDescription.Should().Contain("Conflict");
        requests.Should().Be(4); // 3 retries + 1 requests
    }

    [Fact]
    public async Task GivenGetDialogReturnsPreconditionFailedThenDlqAfter3Retries()
    {
        // Arrange
        var arrangement = ArrangeDefaults();
        var dialogId = arrangement.DialogId;

        _application.DialogportenApi
            .Given(Request.Create().DpGetDialog(dialogId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.PreconditionFailed));

        // Act
        await Send(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        var dlqMessage = await WaitForDlqMessage();
        var getRequests = _application.DialogportenApi.FindLogEntries(Request.Create().DpGetDialog(dialogId)).Count;
        var postRequests = _application.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;

        dlqMessage.Should().NotBeNull();
        dlqMessage.DeadLetterErrorDescription.Should().Contain("Precondition Failed");
        getRequests.Should().Be(4); // 3 retries + 1 requests
        postRequests.Should().Be(0);
    }

    [Fact]
    public async Task GivenPostDialogReturnsUnprocessableEntityThenDlqAfter9Retries()
    {
        // Arrange
        var arrangement = ArrangeDefaults();

        _application.DialogportenApi
            .Given(Request.Create().DpPostDialog())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.UnprocessableEntity));

        // Act
        await Send(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        var dlqMessage = await WaitForDlqMessage();
        var requests = _application.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;

        dlqMessage.Should().NotBeNull();
        dlqMessage.DeadLetterErrorDescription.Should().Contain("Unprocessable Entity");
        requests.Should().Be(10); // 9 retries + 1 requests
    }

    [Fact]
    public async Task GivenNoPartiesFoundInRegisterThenDlqAfter6Retries()
    {
        // Arrange
        await _application.AppScope.ServiceProvider
            .GetRequiredService<IFusionCache>()
            .ClearAsync(token: TestContext.Current.CancellationToken);

        var arrangement = ArrangeDefaults();

        _application.RegisterApi
            .Given(Request.Create().RegisterPostPartySearch())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(new PartyQueryResponse([]))));

        // Act
        await Send(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        var dlqMessage = await WaitForDlqMessage();
        var searchRequests = _application.RegisterApi.FindLogEntries(Request.Create().RegisterPostPartySearch()).Count;
        var postRequests = _application.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;

        dlqMessage.Should().NotBeNull();
        dlqMessage.DeadLetterErrorDescription.Should().Contain("Party with id 2 not found");
        searchRequests.Should().Be(14); // (6 retries + 1 requests) * 2 events
        postRequests.Should().Be(0);
    }

    [Fact]
    public async Task GivenUnauthorizedThenDlqAfter3Retries()
    {
        // Arrange
        var arrangement = ArrangeDefaults();
        var dialogId = arrangement.DialogId;

        _application.DialogportenApi
            .Given(Request.Create().DpGetDialog(dialogId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.Unauthorized));

        // Act
        await Send(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        var dlqMessage = await WaitForDlqMessage();
        var getRequests = _application.DialogportenApi.FindLogEntries(Request.Create().DpGetDialog(dialogId)).Count;
        var postRequests = _application.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;

        dlqMessage.Should().NotBeNull();
        dlqMessage.DeadLetterErrorDescription.Should().Contain("Unauthorized");
        getRequests.Should().Be(8); // (3 retries + 1 requests) * 2 extra request after token refresh attempt
        postRequests.Should().Be(0);
    }

    [Fact]
    public async Task GivenGetDialogDownRetryIndefinitely()
    {
        // Arrange
        var arrangement = ArrangeDefaults();
        var dialogId = arrangement.DialogId;

        _application.DialogportenApi
            .Given(Request.Create().DpGetDialog(dialogId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.ServiceUnavailable));

        // Act
        await Send(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        var dlqMessage = WaitForDlqMessage(TimeSpan.FromMilliseconds(5000));
        var dialogLog = WaitForDialogPostedLogEntry(TimeSpan.FromMilliseconds(5000));
        await Task.WhenAll(dlqMessage, dialogLog);

        dlqMessage?.Result.Should().BeNull();
        dialogLog?.Result.Should().BeNull();
        var failedRequests = _application.DialogportenApi.FindLogEntries(Request.Create().DpGetDialog(dialogId)).Count;

        failedRequests.Should().BeGreaterThan(5);
    }
}

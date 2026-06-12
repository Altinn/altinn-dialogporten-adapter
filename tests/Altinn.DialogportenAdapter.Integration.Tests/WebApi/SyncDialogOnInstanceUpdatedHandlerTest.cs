using System.Net;
using System.Text.Json;
using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.Integration.Tests.Common;
using Altinn.DialogportenAdapter.Integration.Tests.Common.Extensions;
using Altinn.DialogportenAdapter.Test.Common.Builder;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.Platform.Storage.Interface.Models;
using AwesomeAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace Altinn.DialogportenAdapter.Integration.Tests.WebApi;

[Collection(nameof(AdapterCollectionFixture))]
public class SyncDialogOnInstanceUpdatedHandlerTest(DialogportenAdapterApplication app)
    : BaseAdapterIntegrationTest(app)
{
    private readonly DialogportenAdapterApplication _app = app;

    [Fact]
    public async Task GivenAllApisWorkProperlyThenDialogIsSaved()
    {
        // Arrange
        var arrangement = ArrangeDefaults();

        // Act
        var result = await SendAndWait(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        result.ShouldBeSuccessful();
        var logEntries = _app.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog());

        logEntries.Count.Should().Be(1);
        logEntries[0].RequestMessage!.Deserialize<DialogDto>().Should().NotBeNull();
    }

    [Fact]
    public async Task GivenGetInstanceReturnsNotFoundThenDialogIsPurged()
    {
        // Arrange
        var arrangement = ArrangeDefaults();

        _app.DialogportenApi
            .Given(Request.Create().DpGetDialog(arrangement.DialogId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(new DialogDto
                {
                    Revision = Guid.NewGuid()
                })));

        _app.StorageApi
            .Given(Request.Create().StorageGetInstance(arrangement.PartyId, arrangement.InstanceId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.NotFound));

        _app.DialogportenApi
            .Given(Request.Create().DpPurgeDialog(arrangement.DialogId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK));

        // Act
        var result = await SendAndWait(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        result.ShouldBeSuccessful();
        var purgeReqs = _app.DialogportenApi.FindLogEntries(Request.Create().DpPurgeDialog(arrangement.DialogId)).Count;
        var postReqs = _app.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;
        purgeReqs.Should().Be(1);
        postReqs.Should().Be(0);
    }

    [Fact]
    public async Task GivenSoftDeletedInstanceAndExistingDialogThenDialogIsDeletedAndNotCreated()
    {
        // Arrange
        var arrangement = ArrangeDefaults();

        _app.DialogportenApi
            .Given(Request.Create().DpGetDialog(arrangement.DialogId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(new DialogDto
                {
                    Revision = Guid.NewGuid()
                })));

        _app.StorageApi
            .Given(Request.Create().StorageGetInstance(arrangement.PartyId, arrangement.InstanceId))
            .AtPriority(short.MaxValue - 1)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(AltinnInstanceBuilder
                    .NewInProgressInstance()
                    .WithCreatedBy(TestContext.Current.Test!.TestDisplayName)
                    .WithInstanceOwner(new InstanceOwner
                    {
                        PartyId = $"{arrangement.PartyId}",
                    })
                    .WithStatus(new InstanceStatus
                    {
                        IsSoftDeleted = true,
                    })
                    .Build())));

        _app.DialogportenApi
            .Given(Request.Create().DpDeleteDialog(arrangement.DialogId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK));

        // Act
        var result = await SendAndWait(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        result.ShouldBeSuccessful();
        var deleteReqs = _app.DialogportenApi
            .FindLogEntries(Request.Create().DpDeleteDialog(arrangement.DialogId))
            .Count;
        var postReq = _app.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;

        deleteReqs.Should().Be(1);
        postReq.Should().Be(0);
    }

    [Fact]
    public async Task GivenSoftDeletedInstanceAndNoExistingDialogThenDialogIsCreatedAndDeleted()
    {
        // Arrange
        var arrangement = ArrangeDefaults();

        _app.StorageApi
            .Given(Request.Create().StorageGetInstance(arrangement.PartyId, arrangement.InstanceId))
            .AtPriority(short.MaxValue - 1)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(AltinnInstanceBuilder
                    .NewInProgressInstance()
                    .WithCreatedBy(TestContext.Current.Test!.TestDisplayName)
                    .WithInstanceOwner(new InstanceOwner
                    {
                        PartyId = $"{arrangement.PartyId}",
                    })
                    .WithStatus(new InstanceStatus
                    {
                        IsSoftDeleted = true,
                    })
                    .Build())));

        _app.DialogportenApi
            .Given(Request.Create().DpDeleteDialog(arrangement.DialogId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK));

        // Act
        var result = await SendAndWait(new SyncInstanceCommand(
            AppId: arrangement.AppId,
            PartyId: $"{arrangement.PartyId}",
            InstanceId: arrangement.InstanceId,
            InstanceCreatedAt: arrangement.InstanceCreatedAt,
            IsMigration: false
        ));

        // Assert
        result.ShouldBeSuccessful();
        var postReq = _app.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;
        var deleteReqs = _app.DialogportenApi
            .FindLogEntries(Request.Create().DpDeleteDialog(arrangement.DialogId))
            .Count;

        postReq.Should().Be(1);
        deleteReqs.Should().Be(1);
    }
}

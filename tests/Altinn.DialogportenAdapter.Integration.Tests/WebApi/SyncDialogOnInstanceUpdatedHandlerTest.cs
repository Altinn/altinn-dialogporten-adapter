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
    public async Task GivenGetInstanceReturnsNotFoundAndExistingDialogThenDialogIsPurged()
    {
        // Arrange
        var arrangement = ArrangeDefaults();
        var dialogId = arrangement.DialogId;

        _app.DialogportenApi
            .Given(Request.Create().DpGetDialog(dialogId))
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
            .Given(Request.Create().DpPurgeDialog(dialogId))
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
        var purgeReqs = _app.DialogportenApi.FindLogEntries(Request.Create().DpPurgeDialog(dialogId)).Count;
        var postReqs = _app.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;
        purgeReqs.Should().Be(1);
        postReqs.Should().Be(0);
    }

    [Fact]
    public async Task GivenPurgeDialogReturnsNotFoundThenAssumeAlreadyPurged()
    {
        // Arrange
        var arrangement = ArrangeDefaults();
        var dialogId = arrangement.DialogId;

        _app.DialogportenApi
            .Given(Request.Create().DpGetDialog(dialogId))
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
            .Given(Request.Create().DpPurgeDialog(dialogId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.NotFound)
                .WithBody(
                    $$"""
                              {
                                "type": "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4",
                                "title": "Resource not found.",
                                "status": 404,
                                "instance": "/api/v1/serviceowner/dialogs/{{dialogId}}/actions/purge",
                                "errors": {
                                  "DialogEntity": [
                                    "Entity 'DialogEntity' with the following key(s) was not found: ({{dialogId}})."
                                  ]
                                },
                                "traceId": "00-edeb32504e8b90d0ed578c6647316daf-2e15b2a2598daa08-00"
                              }
                      """)
            );

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
        var purgeReqs = _app.DialogportenApi.FindLogEntries(Request.Create().DpPurgeDialog(dialogId)).Count;
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

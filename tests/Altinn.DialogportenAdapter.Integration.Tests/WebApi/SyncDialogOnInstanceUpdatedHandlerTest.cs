using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.Integration.Tests.Common;
using Altinn.DialogportenAdapter.Integration.Tests.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using AwesomeAssertions;
using WireMock.RequestBuilders;
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
}

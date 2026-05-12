using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.Integration.Tests.Common;
using Altinn.DialogportenAdapter.Integration.Tests.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using AwesomeAssertions;
using WireMock.RequestBuilders;
using Xunit;

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
        var dialog = await WaitForRequestOrFail<DialogDto>(Request.Create().DpPostDialog(), 201);
        var requests = _application.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog()).Count;
        dialog.Should().NotBeNull();
        requests.Should().Be(1);
    }
}

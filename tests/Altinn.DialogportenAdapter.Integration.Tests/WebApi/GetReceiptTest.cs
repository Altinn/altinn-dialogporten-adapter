using System.Net;
using System.Text.Json;
using Altinn.DialogportenAdapter.Integration.Tests.Common;
using Altinn.DialogportenAdapter.Integration.Tests.Common.Extensions;
using Altinn.DialogportenAdapter.Test.Common.Builder;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using Altinn.Platform.Storage.Interface.Models;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using Xunit;
using Response = WireMock.ResponseBuilders.Response;

namespace Altinn.DialogportenAdapter.Integration.Tests.WebApi;

[Collection(nameof(AdapterCollectionFixture))]
public class GetReceiptTest(DialogportenAdapterApplication app) : BaseAdapterIntegrationTest(app)
{
    private readonly DialogportenAdapterApplication _app = app;

    [Fact]
    public async Task GivenPreflightRequestFromAllowedOriginReturnsExpectedAccessControlHeaders()
    {
        // Act
        var clientFactory = _app.App.Services.GetRequiredService<IHttpClientFactory>();
        using var client = clientFactory.CreateClient();
        var url = $"{GetHostUri()}/storage/dialogporten/api/v1/receipt/{Guid.NewGuid()}/{Guid.NewGuid()}?lang=en";
        var request = new HttpRequestMessage(HttpMethod.Options, url);
        request.Headers.Add("Authorization", "Bearer ignored");
        request.Headers.Add("Origin", "af.altinn.no");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Authorization, Prefer");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        response.Headers.GetValues("Access-Control-Allow-Origin").Single().Should().Be("*");
        response.Headers.GetValues("Access-Control-Allow-Methods").Single().Should().Be("GET");
        response.Headers.GetValues("Access-Control-Allow-Headers").Should().BeEquivalentTo("Authorization,Prefer");
    }

    [Fact]
    public async Task GivenGetReceiptCreatesAReceipt()
    {
        // Arrange
        var clientFactory = _app.App.Services.GetRequiredService<IHttpClientFactory>();
        using var client = clientFactory.CreateClient();
        ArrangeDefaultsForReceipt(out var dialogId, out var transmissionId);

        // Act
        var url = $"{GetHostUri()}/storage/dialogporten/api/v1/receipt/{dialogId}/{transmissionId}?lang=en";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", "Bearer ignored");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Be("""
                          | **Date sent:** | **01.01.2020 / 12:30** |
                          |:-|:-|
                          | **Sender:** | 029147*****-testName |
                          | **Receiver:** | 991825827 |
                          | **Reference number:** | 87e518ebf653 |

                          A mechanical check has been completed while filling in, but we reserve the right to detect errors during the processing of the case and that other documentation may be necessary. Please provide the reference number in case of any inquiries to the agency.
                          """);
    }

    [Fact]
    public async Task GivenPreferHeaderWithTimezoneCreatesAReceiptWithRequestedTimezone()
    {
        // Arrange
        var clientFactory = _app.App.Services.GetRequiredService<IHttpClientFactory>();
        using var client = clientFactory.CreateClient();
        ArrangeDefaultsForReceipt(out var dialogId, out var transmissionId);

        // Act
        var url = $"{GetHostUri()}/storage/dialogporten/api/v1/receipt/{dialogId}/{transmissionId}?lang=en";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", "Bearer ignored");
        request.Headers.Add("Prefer", "timezone=Europe/Oslo");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var europeOsloTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");
        var expectedTime = europeOsloTimeZone.IsDaylightSavingTime(DateTime.Now)
            ? "01.01.2020 / 13:30"
            : "01.01.2020 / 14:30";

        body.Should().Be($"""
                          | **Date sent:** | **{expectedTime}** |
                          |:-|:-|
                          | **Sender:** | 029147*****-testName |
                          | **Receiver:** | 991825827 |
                          | **Reference number:** | 87e518ebf653 |

                          A mechanical check has been completed while filling in, but we reserve the right to detect errors during the processing of the case and that other documentation may be necessary. Please provide the reference number in case of any inquiries to the agency.
                          """);
    }

    private void ArrangeDefaultsForReceipt(out Guid dialogId, out Guid transmissionId)
    {
        dialogId = Guid.Parse("6a6a0c9e-9072-45bd-9b9b-13119dc0356e");
        transmissionId = Guid.Parse("407d3e62-078b-49a5-a7a3-84fb58b6aa16");
        var partyId = 50123456;
        var instanceId = Guid.Parse("4a92385d-cd09-4b0e-9749-87e518ebf653");
        var org = "991825827";
        var appId = $"{org}/123";
        var partyUrn = "urn:altinn:person:identifier-no:02914797589";

        var transmissionCreatedAt = new DateTimeOffset(2020, 1, 1, 12, 30, 0, TimeSpan.Zero);
        _app.DialogportenApi
            .Given(Request.Create().DpGetDialog(dialogId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(new DialogDto
                {
                    Org = org,
                    Party = partyUrn,
                    ServiceOwnerContext = new ServiceOwnerContext
                    {
                        ServiceOwnerLabels =
                        [
                            new ServiceOwnerLabel
                            {
                                Value = $"urn:altinn:integration:storage:{partyId}/{instanceId}"
                            }
                        ]
                    },
                    Transmissions =
                    [
                        new TransmissionDto
                        {
                            Id = transmissionId,
                            CreatedAt = transmissionCreatedAt
                        }
                    ]
                })));

        _app.StorageApi
            .Given(Request.Create().StorageGetInstance(partyId, instanceId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(AltinnInstanceBuilder
                    .NewInProgressInstance()
                    .WithId(instanceId.ToString())
                    .WithAppId(appId)
                    .Build())));

        _app.StorageApi
            .Given(Request.Create().StorageGetApplication(appId))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(
                    AltinnApplicationBuilder
                        .NewDefaultAltinnApplication()
                        .WithVersionId("versionId")
                        .Build()
                )));

        _app.RegisterApi
            .Given(Request.Create().RegisterPostPartySearch())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(new PartyQueryResponse([
                    new PartyIdentifier(
                        PartyId: partyId,
                        PartyType: "person",
                        PersonIdentifier: "02914797589",
                        OrganizationIdentifier: null,
                        ExternalUrn: partyUrn,
                        DisplayName: "testName"
                    )
                ]))));

        _app.StorageApi
            .Given(Request.Create().StorageGetApplicationTexts(appId, "nb"))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(CreateTextResource("nb"))));
        _app.StorageApi
            .Given(Request.Create().StorageGetApplicationTexts(appId, "nn"))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(CreateTextResource("nn"))));
        _app.StorageApi
            .Given(Request.Create().StorageGetApplicationTexts(appId, "en"))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(CreateTextResource("en"))));
    }

    private static TextResource CreateTextResource(string language)
    {
        return new TextResource()
        {
            Id = "1",
            Org = "skd",
            Language = language,
            Resources =
            [
                new TextResourceElement
                {
                    Id = InstanceReceipt.InstanceReceiptSummaryKey,
                    Value = "summary",
                    Variables = null
                }
            ]
        };
    }
}

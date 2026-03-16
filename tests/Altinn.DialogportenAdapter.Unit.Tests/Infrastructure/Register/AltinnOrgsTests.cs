using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using NSubstitute;
using ZiggyCreatures.Caching.Fusion;

namespace Altinn.DialogportenAdapter.Unit.Tests.Infrastructure.Register;

public class AltinnOrgsTests
{
    private const string JsonPayload = """
       {
         "orgs": {
           "digdir": {
             "name": {
               "en": "Norwegian Digitalisation Agency",
               "nb": "Digitaliseringsdirektoratet",
               "nn": "Digitaliseringsdirektoratet"
             },
             "logo": "https://altinncdn.no/orgs/digdir/digdir.png",
             "emblem": "https://altinncdn.no/orgs/digdir/digdir.svg",
             "orgnr": "991825827",
             "homepage": "https://www.digdir.no",
             "environments": ["tt02", "production"],
             "contact": {
               "phone": "+4722451000",
               "url": "https://www.digdir.no/digdir/kontakt-oss/943"
             }
           },
           "brg": {
             "name": {
               "en": "Brønnøysund Register Centre",
               "nb": "Brønnøysundregistrene",
               "nn": "Brønnøysundregistera"
             },
             "logo": "https://altinncdn.no/orgs/brg/brreg.png",
             "orgnr": "974760673",
             "homepage": "https://www.brreg.no",
             "environments": ["tt02", "production"]
           }
         }
       }
       """;

    [Fact]
    public async Task GetAltinnOrgs_CheckMaping()
    {
        var expected = new AltinnOrgData(
            new Dictionary<string, Org>
            {
                ["digdir"] = new Org(
                    Name: new Dictionary<string, string>
                    {
                        ["en"] = "Norwegian Digitalisation Agency",
                        ["nb"] = "Digitaliseringsdirektoratet",
                        ["nn"] = "Digitaliseringsdirektoratet"
                    },
                    OrgNr: "991825827",
                    Environments: ["tt02", "production"],
                    Logo: "https://altinncdn.no/orgs/digdir/digdir.png",
                    Emblem: "https://altinncdn.no/orgs/digdir/digdir.svg",
                    HomePage: "https://www.digdir.no",
                    Contact: new OrgContact(
                        Phone: "+4722451000",
                        Url: "https://www.digdir.no/digdir/kontakt-oss/943"
                    )
                ),
                ["brg"] = new Org(
                    Name: new Dictionary<string, string>
                    {
                        ["en"] = "Brønnøysund Register Centre",
                        ["nb"] = "Brønnøysundregistrene",
                        ["nn"] = "Brønnøysundregistera"
                    },
                    OrgNr: "974760673",
                    Environments: ["tt02", "production"],
                    Logo: "https://altinncdn.no/orgs/brg/brreg.png",
                    Emblem: null,
                    HomePage: "https://www.brreg.no",
                    Contact: null
                )
            }
        );

        var handler = new StubHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonPayload, Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);

        var result = await sut.GetAltinnOrgs(CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetAltinnOrgs_TwoCalls_UsesCacheAndCallsHttpOnce()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonPayload, Encoding.UTF8, "application/json")
            });

        var sut = CreateSut(handler);

        var first = await sut.GetAltinnOrgs(CancellationToken.None);
        var second = await sut.GetAltinnOrgs(CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAltinnOrgs_NullPayload_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create<AltinnOrgData?>(null)
            });

        var sut = CreateSut(handler);

        var result = await sut.GetAltinnOrgs(CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAltinnOrgs_HttpFailure_ThrowsHttpRequestException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var sut = CreateSut(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetAltinnOrgs(CancellationToken.None));
    }

    private static AltinnOrgs CreateSut(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var clientFactory = Substitute.For<IHttpClientFactory>();
        clientFactory
            .CreateClient(Constants.AltinnOrgsClient)
            .Returns(httpClient);

        return new AltinnOrgs(new FusionCache(new FusionCacheOptions()), clientFactory);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handle)
        : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestUri = request.RequestUri;
            var response = handle(request, cancellationToken);
            return Task.FromResult(response);
        }
    }
}

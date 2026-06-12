using System.Net;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;
using NSubstitute;
using Refit;
using ZiggyCreatures.Caching.Fusion;

namespace Altinn.DialogportenAdapter.Unit.Tests.Infrastructure.Storage;

public class ApplicationRepositoryTests
{
    [Fact]
    public void CreateTextsDictionary_KeepsFirstOccurrenceForDuplicateIds()
    {
        var resources = new List<TextResourceElement>
        {
            new() { Id = "dp.title", Value = "first-title" },
            new() { Id = "dp.title", Value = "second-title" },
            new() { Id = "dp.summary", Value = "summary" }
        };

        var result = ApplicationRepository.CreateTextsDictionary(resources);

        Assert.Equal(2, result.Count);
        Assert.Equal("first-title", result["dp.title"]);
        Assert.Equal("summary", result["dp.summary"]);
    }

    [Fact]
    public async Task GetApplicationTexts_OneApiError_ThrowsAnApiException()
    {
        var applicationsApi = Substitute.For<IApplicationsApi>();
        var repository = new ApplicationRepository(applicationsApi, new FusionCache(new FusionCacheOptions()));
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        applicationsApi
            .GetApplicationTexts("123", "123456789", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                CreateApplicationTextSuccessApiResponse("nb"),
                CreateApplicationTextSuccessApiResponse("nn"),
                new ApiResponse<TextResource>(
                    httpResponseMessage,
                    null,
                    new RefitSettings(),
                    await ApiException.Create(
                        new HttpRequestMessage(HttpMethod.Get, "http://example.com"),
                        HttpMethod.Get,
                        httpResponseMessage,
                        new RefitSettings()
                    )
                )
            );

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            repository.GetApplicationTexts("123/123456789", "1",  CancellationToken.None)
        );

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);

        await applicationsApi.Received().GetApplicationTexts("123", "123456789", "nb", Arg.Any<CancellationToken>());
        await applicationsApi.Received().GetApplicationTexts("123", "123456789", "nn", Arg.Any<CancellationToken>());
        await applicationsApi.Received().GetApplicationTexts("123", "123456789", "en", Arg.Any<CancellationToken>());

        Assert.Equal(3, applicationsApi.ReceivedCalls().Count());
    }

    [Fact]
    public async Task GetApplicationTexts_LanguageIsFound_ReturnsTranslations()
    {
        var applicationsApi = Substitute.For<IApplicationsApi>();
        var repository = new ApplicationRepository(applicationsApi, new FusionCache(new FusionCacheOptions()));
        var languages = new List<string> { "nb", "nn", "en" };
        languages.ForEach(language =>
        {
            applicationsApi
                .GetApplicationTexts("123", "123456789", language, Arg.Any<CancellationToken>())
                .Returns(CreateApplicationTextSuccessApiResponse(language));
        });

        var result = await repository.GetApplicationTexts("123/123456789", "1", CancellationToken.None);
        Assert.Equal(languages.Count, result.Translations.Count);

        await applicationsApi.Received().GetApplicationTexts("123", "123456789", "nb", Arg.Any<CancellationToken>());
        await applicationsApi.Received().GetApplicationTexts("123", "123456789", "nn", Arg.Any<CancellationToken>());
        await applicationsApi.Received().GetApplicationTexts("123", "123456789", "en", Arg.Any<CancellationToken>());

        Assert.Equal(3, applicationsApi.ReceivedCalls().Count());
    }

    [Fact]
    public async Task GetApplicationTexts_LanguageIsNotFound_ReturnsEmptyArray()
    {
        var applicationsApi = Substitute.For<IApplicationsApi>();
        var repository = new ApplicationRepository(applicationsApi, new FusionCache(new FusionCacheOptions()));
        applicationsApi.GetApplicationTexts("123", "123456789", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                new ApiResponse<TextResource>(
                    new HttpResponseMessage(HttpStatusCode.NotFound),
                    null,
                    new RefitSettings()
                )
            );

        var result = await repository.GetApplicationTexts("123/123456789", "1", CancellationToken.None);
        Assert.Empty(result.Translations);

        await applicationsApi.Received().GetApplicationTexts("123", "123456789", "nb", Arg.Any<CancellationToken>());
        await applicationsApi.Received().GetApplicationTexts("123", "123456789", "nn", Arg.Any<CancellationToken>());
        await applicationsApi.Received().GetApplicationTexts("123", "123456789", "en", Arg.Any<CancellationToken>());

        Assert.Equal(3, applicationsApi.ReceivedCalls().Count());
    }

    private static ApiResponse<TextResource> CreateApplicationTextSuccessApiResponse(string language)
    {
        return new ApiResponse<TextResource>(
            new HttpResponseMessage(HttpStatusCode.OK),
            new TextResource
            {
                Id = "id",
                Org = "org",
                Language = language,
                Resources =
                [
                    new TextResourceElement
                    {
                        Id = "text-resource-element-id",
                        Value = "text-resource-element-value",
                        Variables =
                        [
                            new TextResourceVariable
                            {
                                Key = "text-resource-variable-key",
                                DataSource = "text-resource-variable-dataSource",
                                DefaultValue = "text-resource-variable-default-values",
                            }
                        ]
                    }
                ]
            },
            new RefitSettings()
        );
    }

    [Fact]
    public async Task GetApplicationTexts_AppAndVersionIsUnchanged_ReturnsCachedTexts()
    {
        var applicationsApi = Substitute.For<IApplicationsApi>();
        var repository = new ApplicationRepository(applicationsApi, new FusionCache(new FusionCacheOptions()));
        var languages = new List<string> { "nb", "nn", "en" };
        languages.ForEach(language =>
        {
            applicationsApi
                .GetApplicationTexts("123", "123456789", language, Arg.Any<CancellationToken>())
                .Returns(CreateApplicationTextSuccessApiResponse(language));
        });

        var result1 = await repository.GetApplicationTexts("123/123456789", "1", CancellationToken.None);
        var result2 = await repository.GetApplicationTexts("123/123456789", "1", CancellationToken.None);
        Assert.Equal(languages.Count, result1.Translations.Count);
        Assert.Equal(languages.Count, result2.Translations.Count);

        await applicationsApi.Received().GetApplicationTexts("123", "123456789", "nb", Arg.Any<CancellationToken>());
        await applicationsApi.Received().GetApplicationTexts("123", "123456789", "nn", Arg.Any<CancellationToken>());
        await applicationsApi.Received().GetApplicationTexts("123", "123456789", "en", Arg.Any<CancellationToken>());

        Assert.Equal(3, applicationsApi.ReceivedCalls().Count());
    }

    [Fact]
    public async Task GetApplicationTexts_VersionNull_CachesTexts()
    {
        var applicationsApi = Substitute.For<IApplicationsApi>();
        var repository = new ApplicationRepository(applicationsApi, new FusionCache(new FusionCacheOptions()));
        var languages = new List<string> { "nb", "nn", "en" };
        languages.ForEach(language =>
        {
            applicationsApi
                .GetApplicationTexts("123", "123456789", language, Arg.Any<CancellationToken>())
                .Returns(CreateApplicationTextSuccessApiResponse(language));
        });

        var result1 = await repository.GetApplicationTexts("123/123456789", null, CancellationToken.None);
        var result2 = await repository.GetApplicationTexts("123/123456789", null, CancellationToken.None);
        Assert.Equal(languages.Count, result1.Translations.Count);
        Assert.Equal(languages.Count, result2.Translations.Count);

        await applicationsApi.Received(1).GetApplicationTexts("123", "123456789", "nb", Arg.Any<CancellationToken>());
        await applicationsApi.Received(1).GetApplicationTexts("123", "123456789", "nn", Arg.Any<CancellationToken>());
        await applicationsApi.Received(1).GetApplicationTexts("123", "123456789", "en", Arg.Any<CancellationToken>());

        Assert.Equal(3, applicationsApi.ReceivedCalls().Count());
    }

    [Fact]
    public async Task GetApplicationTexts_VersionChanged_ReturnsNewTexts()
    {
        var applicationsApi = Substitute.For<IApplicationsApi>();
        var repository = new ApplicationRepository(applicationsApi, new FusionCache(new FusionCacheOptions()));
        var languages = new List<string> { "nb", "nn", "en" };
        languages.ForEach(language =>
        {
            applicationsApi
                .GetApplicationTexts("123", "123456789", language, Arg.Any<CancellationToken>())
                .Returns(CreateApplicationTextSuccessApiResponse(language));
        });

        var result1 = await repository.GetApplicationTexts("123/123456789", "1", CancellationToken.None);
        var result2 = await repository.GetApplicationTexts("123/123456789", "2", CancellationToken.None);
        Assert.Equal(languages.Count, result1.Translations.Count);
        Assert.Equal(languages.Count, result2.Translations.Count);

        await applicationsApi.Received(2).GetApplicationTexts("123", "123456789", "nb", Arg.Any<CancellationToken>());
        await applicationsApi.Received(2).GetApplicationTexts("123", "123456789", "nn", Arg.Any<CancellationToken>());
        await applicationsApi.Received(2).GetApplicationTexts("123", "123456789", "en", Arg.Any<CancellationToken>());

        Assert.Equal(6, applicationsApi.ReceivedCalls().Count());
    }

    [Fact]
    public async Task GetApplicationTexts_AppChanged_ReturnsNewTexts()
    {
        var applicationsApi = Substitute.For<IApplicationsApi>();
        var repository = new ApplicationRepository(applicationsApi, new FusionCache(new FusionCacheOptions()));
        var languages = new List<string> { "nb", "nn", "en" };
        languages.ForEach(language =>
        {
            applicationsApi
                .GetApplicationTexts("123", "123456789", language, Arg.Any<CancellationToken>())
                .Returns(CreateApplicationTextSuccessApiResponse(language));
            applicationsApi
                .GetApplicationTexts("123", "223456789", language, Arg.Any<CancellationToken>())
                .Returns(CreateApplicationTextSuccessApiResponse(language));
        });

        var result1 = await repository.GetApplicationTexts("123/123456789", "1", CancellationToken.None);
        var result2 = await repository.GetApplicationTexts("123/223456789", "1", CancellationToken.None);
        Assert.Equal(languages.Count, result1.Translations.Count);
        Assert.Equal(languages.Count, result2.Translations.Count);

        await applicationsApi.Received().GetApplicationTexts("123", "123456789", "nb", Arg.Any<CancellationToken>());
        await applicationsApi.Received().GetApplicationTexts("123", "123456789", "nn", Arg.Any<CancellationToken>());
        await applicationsApi.Received().GetApplicationTexts("123", "123456789", "en", Arg.Any<CancellationToken>());
        await applicationsApi.Received().GetApplicationTexts("123", "223456789", "nb", Arg.Any<CancellationToken>());
        await applicationsApi.Received().GetApplicationTexts("123", "223456789", "nn", Arg.Any<CancellationToken>());
        await applicationsApi.Received().GetApplicationTexts("123", "223456789", "en", Arg.Any<CancellationToken>());

        Assert.Equal(6, applicationsApi.ReceivedCalls().Count());
    }
}

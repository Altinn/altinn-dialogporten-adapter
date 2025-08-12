using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.Platform.Storage.Interface.Models;
using Microsoft.Extensions.Caching.Hybrid;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;

internal class CachingApplicationsApi : ICachingApplicationsApi
{
    private readonly IApplicationsApi _applicationsApi;
    private readonly HybridCache _cache;
    private static readonly string[] PredefinedLanguages = ["nb", "nn", "en"];

    public CachingApplicationsApi(IApplicationsApi applicationsApi, HybridCache cache)
    {
        _applicationsApi = applicationsApi;
        _cache = cache;
    }

    public async Task<Application?> GetApplication(string appId, CancellationToken cancellationToken = default) =>
        await _cache.GetOrCreateAsync(
            $"metadata-{appId}",
            async ct => await _applicationsApi.GetApplication(appId, ct).ContentOrDefault(),
            cancellationToken: cancellationToken
        );

    public async Task<ApplicationTexts> GetApplicationTexts(string appId, CancellationToken cancellationToken = default) =>
        await _cache.GetOrCreateAsync(
            $"texts-{appId}",
            async ct =>
            {
                var orgApp = appId.Split('/');
                var tasks = PredefinedLanguages.Select(lang => _applicationsApi.GetApplicationTexts(orgApp[0], orgApp[1], lang, ct));
                var responses = await Task.WhenAll(tasks);

                var textResources = responses
                    .Where(response => response.IsSuccessful)
                    .Select(response => response.Content!)
                    .ToList();

                return new ApplicationTexts
                {
                    Translations = textResources.Select(textResource => new ApplicationTextsTranslation
                    {
                        Language = textResource.Language,
                        Texts = textResource.Resources.ToDictionary(x => x.Id, x => x.Value)
                    }).ToList()
                };
            },
            cancellationToken: cancellationToken);

}


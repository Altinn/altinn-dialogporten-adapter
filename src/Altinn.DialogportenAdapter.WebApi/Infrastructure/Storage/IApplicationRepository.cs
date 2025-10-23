using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.Platform.Storage.Interface.Models;
using ZiggyCreatures.Caching.Fusion;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;

internal interface IApplicationRepository
{
    Task<Application?> GetApplication(string appId, CancellationToken cancellationToken);
    Task<ApplicationTexts> GetApplicationTexts(string appId, CancellationToken cancellationToken);
}

internal sealed class ApplicationRepository(IApplicationsApi applicationsApi, IFusionCache cache) : IApplicationRepository
{
    public Task<Application?> GetApplication(string appId, CancellationToken cancellationToken) =>
        cache.GetOrSetAsync(
            key: $"{nameof(Application)}:{appId}",
            factory: (ct) => FetchApplication(appId, ct),
            token: cancellationToken).AsTask();

    public Task<ApplicationTexts> GetApplicationTexts(string appId, CancellationToken cancellationToken) =>
        cache.GetOrSetAsync(
            key: $"{nameof(ApplicationTexts)}:{appId}",
            factory: (ct) => FetchApplicationTexts(appId, ct),
            token: cancellationToken).AsTask();

    private async Task<ApplicationTexts> FetchApplicationTexts(string appId, CancellationToken cancellationToken)
    {
        string[] predefinedLanguages = ["nb", "nn", "en"];
        var orgApp = appId.Split('/');
        var tasks = predefinedLanguages.Select(lang => applicationsApi.GetApplicationTexts(orgApp[0], orgApp[1], lang, cancellationToken));
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
    }

    private async Task<Application?> FetchApplication(string appId, CancellationToken cancellationToken) =>
        await applicationsApi.GetApplication(appId, cancellationToken).ContentOrDefault();
}

using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.Platform.Storage.Interface.Models;
using ZiggyCreatures.Caching.Fusion;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;

internal interface IApplicationRepository
{
    Task<Application?> GetApplication(string appId, CancellationToken cancellationToken);
    Task<(bool, Application?)> TryGetApplicationIfCached(string appId, CancellationToken cancellationToken);
    Task<ApplicationTexts> GetApplicationTexts(string appId, CancellationToken cancellationToken);
}

internal sealed class ApplicationRepository(IApplicationsApi applicationsApi, IFusionCache cache) : IApplicationRepository
{
    public Task<Application?> GetApplication(string appId, CancellationToken cancellationToken) =>
        cache.GetOrSetAsync(
            key: $"{nameof(Application)}:{appId}",
            factory: (ct) => FetchApplication(appId, ct),
            token: cancellationToken).AsTask();

    public async Task<(bool, Application?)> TryGetApplicationIfCached(string appId, CancellationToken cancellationToken)
    {
        var maybeApplication = await cache.TryGetAsync<Application?>(key: $"{nameof(Application)}:{appId}", token: cancellationToken);
        return maybeApplication.HasValue ? (true, maybeApplication.Value) : (false, null);
    }

    public Task<ApplicationTexts> GetApplicationTexts(string appId, CancellationToken cancellationToken) =>
        cache.GetOrSetAsync(
            key: $"{nameof(ApplicationTexts)}:{appId}",
            factory: (ct) => FetchApplicationTexts(appId, ct),
            token: cancellationToken).AsTask();

    private async Task<ApplicationTexts> FetchApplicationTexts(string appId, CancellationToken cancellationToken)
    {
        string[] predefinedLanguages = ["nb", "nn", "en"];
        var orgApp = appId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (orgApp.Length != 2)
        {
            throw new ArgumentException($"Expected appId in 'org/app' format, got '{appId}'.", nameof(appId));
        }
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
                Texts = CreateTextsDictionary(textResource.Resources)
            }).ToList()
        };
    }

    internal static Dictionary<string, string> CreateTextsDictionary(IEnumerable<TextResourceElement> resources)
    {
        var texts = new Dictionary<string, string>();

        foreach (var resource in resources)
        {
            if (resource.Id is null)
            {
                continue;
            }

            texts.TryAdd(resource.Id, resource.Value);
        }

        return texts;
    }

    private async Task<Application?> FetchApplication(string appId, CancellationToken cancellationToken) =>
        await applicationsApi.GetApplication(appId, cancellationToken).ContentOrDefault();
}

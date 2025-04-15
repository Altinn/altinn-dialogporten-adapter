using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;

public interface ICachingApplicationsApi
{
    public Task<Application?> GetApplication(string appId, CancellationToken cancellationToken = default);
    public Task<ApplicationTexts> GetApplicationTexts(string appId, CancellationToken cancellationToken = default);
}

public class ApplicationTexts
{
    public List<ApplicationTextsTranslation> Translations { get; set; } = new();
}

public class ApplicationTextsTranslation
{
    public string Language { get; set; } = string.Empty;
    public Dictionary<string, string> Texts { get; set; } = new();
}
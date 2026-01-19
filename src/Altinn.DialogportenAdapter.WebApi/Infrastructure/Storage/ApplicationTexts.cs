namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;

public class ApplicationTexts
{
    //public List<ApplicationTextsTranslation> Translations { get; set; } = new();
    public Dictionary<string, ApplicationTextsTranslation> Translations { get; set; } = new();
}

public class ApplicationTextsTranslation
{
    public string Language { get; set; } = string.Empty;
    public Dictionary<string, string> Texts { get; set; } = new();
}

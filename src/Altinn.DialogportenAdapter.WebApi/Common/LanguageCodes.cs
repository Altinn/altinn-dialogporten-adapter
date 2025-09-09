using System.Globalization;

namespace Altinn.DialogportenAdapter.WebApi.Common;

// Copied from https://github.com/Altinn/dialogporten/blob/main/src/Digdir.Domain.Dialogporten.Domain/Localizations/Localization.cs
public static class LanguageCodes
{
    private static readonly Dictionary<string, CultureInfo> NeutralCultureByValidCultureCodes =
        BuildNeutralCultureByValidCultureCodes();

    public static bool IsValidCultureCode(string? cultureCode) =>
        cultureCode is not null
        && NeutralCultureByValidCultureCodes.TryGetValue(cultureCode, out var neutralCulture)
        && cultureCode == neutralCulture.TwoLetterISOLanguageName;

    private static Dictionary<string, CultureInfo> BuildNeutralCultureByValidCultureCodes()
    {
        var exclude = new[] { "no", "iv" };
        var cultureGroups = CultureInfo
            .GetCultures(CultureTypes.NeutralCultures | CultureTypes.SpecificCultures)
            .Where(x => !exclude.Contains(x.TwoLetterISOLanguageName))
            .GroupBy(x => x.TwoLetterISOLanguageName)
            .ToList();

        var neutralCultureByValidCultureCodes = new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var cultureGroup in cultureGroups)
        {
            var neutral = cultureGroup.First(x => x.CultureTypes.HasFlag(CultureTypes.NeutralCultures));
            neutralCultureByValidCultureCodes[neutral.TwoLetterISOLanguageName] = neutral;
            neutralCultureByValidCultureCodes[neutral.ThreeLetterISOLanguageName] = neutral;

            foreach (var culture in cultureGroup.Except([neutral]))
            {
                neutralCultureByValidCultureCodes[culture.Name] = neutral;
            }
        }

        return neutralCultureByValidCultureCodes;
    }
}
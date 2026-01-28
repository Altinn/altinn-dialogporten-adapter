using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Features.Command.Sync;

public class StorageDialogportenDataMergerTest
{

    [Fact]
    public void SimpleTest()
    {
        var instance = new Instance
        {
            Process = new ProcessState()
            {
                CurrentTask = new ProcessElementInfo
                {
                    ElementId = "Task1"
                }
            }
        };
        var texts = new ApplicationTexts
        {
            Translations =
            [
                new ApplicationTextsTranslation
                {
                    Language = "nb",
                    Texts = new Dictionary<string, string>
                    {
                        { "dp.title.Task1.awaitingsignature", "wait!" },
                        { "dp.title.Task2.rejected", "begone task 2" },
                        { "dp.title._any_.rejected", "begone task any" }
                    }
                }
            ]
        };
        var localizations = ApplicationTextParser.GetLocalizationsFromApplicationTexts("title", instance, texts, InstanceDerivedStatus.Rejected);

        Assert.Single(localizations);
        Assert.Equal("begone task any", localizations.First().Value);
    }

    [Fact]
    public void TrimStringExceedingMaxLength()
    {
        var instance = new Instance
        {
            Process = new ProcessState()
            {
                CurrentTask = new ProcessElementInfo
                {
                    ElementId = "Task1"
                }
            }
        };
        var texts = new ApplicationTexts
        {
            Translations =
            [
                new ApplicationTextsTranslation
                {
                    Language = "nb",
                    Texts = new Dictionary<string, string>
                    {
                        { "dp.title.Task1.awaitingsignature", new string('a', 1000) },
                        { "dp.summary.Task1.awaitingsignature", new string('b', 1000) },
                        { "dp.summary.Task1.rejected", new string('b', 120) },
                    }
                }
            ]
        };
        
        var localizations = ApplicationTextParser.GetLocalizationsFromApplicationTexts("title", instance, texts, InstanceDerivedStatus.AwaitingSignature);
        var summaryLocalizations = ApplicationTextParser.GetLocalizationsFromApplicationTexts("summary", instance, texts, InstanceDerivedStatus.AwaitingSignature);
        var summaryLocalizationsShort = ApplicationTextParser.GetLocalizationsFromApplicationTexts("summary", instance, texts, InstanceDerivedStatus.Rejected);
        
        Assert.Single(localizations);
        var title = localizations.First().Value;
        Assert.Equal(255, title.Length);
        Assert.EndsWith("a...", title, StringComparison.Ordinal);
        
        
        Assert.Single(summaryLocalizations);
        var summary = summaryLocalizations.First().Value;
        Assert.Equal(255, summary.Length);
        Assert.EndsWith("b...", summary, StringComparison.Ordinal);
        
        
        Assert.Single(summaryLocalizationsShort);
        var summaryShort = summaryLocalizationsShort.First().Value;
        Assert.Equal(120, summaryShort.Length);
        Assert.EndsWith("bbb", summaryShort, StringComparison.Ordinal);
    }
}

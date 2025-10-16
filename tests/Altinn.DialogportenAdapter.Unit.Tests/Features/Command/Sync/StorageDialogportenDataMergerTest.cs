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
}

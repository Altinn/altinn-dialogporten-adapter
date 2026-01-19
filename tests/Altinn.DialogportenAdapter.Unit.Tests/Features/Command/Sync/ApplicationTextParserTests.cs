using System.Collections.Generic;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Features.Command.Sync;

public class ApplicationTextParserTests
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
            Translations = new Dictionary<string, ApplicationTextsTranslation>
            {
                {
                    "nb", new ApplicationTextsTranslation
                    {
                        Language = "nb",
                        Texts = new Dictionary<string, string>
                        {
                            { "dp.title.Task1.awaitingsignature", "wait!" },
                            { "dp.title.Task2.rejected", "begone task 2" },
                            { "dp.title._any_.rejected", "begone task any" }
                        }
                    }
                }
            }
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
            {
                {
                    "nb", new ApplicationTextsTranslation
                    {
                        Language = "nb",
                        Texts = new Dictionary<string, string>
                        {
                            { "dp.title.Task1.awaitingsignature", new string('a', 1000) },
                            { "dp.summary.Task1.awaitingsignature", new string('b', 1000) },
                            { "dp.summary.Task1.rejected", new string('b', 120) },
                        }
                    }
                }
            }
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
    
    [Fact]
    public void ReturnsMostSpecificKeyForTaskAndDerivedStatus()
    {
        var instance = CreateInstance("Task1");
        var texts = CreateTexts(
            ("nb", new Dictionary<string, string>
            {
                { "dp.title.Task1.awaitingsignature", "specific" },
                { "dp.title.Task1", "task-only" },
                { "dp.title._any_.awaitingsignature", "any-status" },
                { "dp.title", "fallback" }
            }));

        var localizations = ApplicationTextParser.GetLocalizationsFromApplicationTexts(
            "title",
            instance,
            texts,
            InstanceDerivedStatus.AwaitingSignature);

        Assert.Collection(localizations, loc =>
        {
            Assert.Equal("nb", loc.LanguageCode);
            Assert.Equal("specific", loc.Value);
        });
    }

    [Fact]
    public void FallsBackToTaskWithoutDerivedStatusWhenStatusSpecificKeyIsMissing()
    {
        var instance = CreateInstance("Task1");
        var texts = CreateTexts(
            ("nb", new Dictionary<string, string>
            {
                { "dp.title.Task1", "task-only" },
                { "dp.title._any_.awaitingsignature", "any-status" },
                { "dp.title", "fallback" }
            }));

        var localizations = ApplicationTextParser.GetLocalizationsFromApplicationTexts(
            "title",
            instance,
            texts,
            InstanceDerivedStatus.AwaitingSignature);

        Assert.Collection(localizations, loc =>
        {
            Assert.Equal("nb", loc.LanguageCode);
            Assert.Equal("task-only", loc.Value);
        });
    }

    [Fact]
    public void UsesAnyTaskEntryForDerivedStatusWhenTaskSpecificIsAbsent()
    {
        var instance = CreateInstance("Task1");
        var texts = CreateTexts(
            ("nb", new Dictionary<string, string>
            {
                { "dp.title._any_.awaitingsignature", "any-status" },
                { "dp.title", "fallback" }
            }));

        var localizations = ApplicationTextParser.GetLocalizationsFromApplicationTexts(
            "title",
            instance,
            texts,
            InstanceDerivedStatus.AwaitingSignature);

        Assert.Collection(localizations, loc =>
        {
            Assert.Equal("nb", loc.LanguageCode);
            Assert.Equal("any-status", loc.Value);
        });
    }

    [Fact]
    public void FallsBackToBaseKeyWhenNoTaskOrStatusSpecificKeysExist()
    {
        var instance = CreateInstance("Task1");
        var texts = CreateTexts(
            ("nb", new Dictionary<string, string>
            {
                { "dp.title", "fallback" }
            }));

        var localizations = ApplicationTextParser.GetLocalizationsFromApplicationTexts(
            "title",
            instance,
            texts,
            InstanceDerivedStatus.AwaitingSignature);

        Assert.Collection(localizations, loc =>
        {
            Assert.Equal("nb", loc.LanguageCode);
            Assert.Equal("fallback", loc.Value);
        });
    }

    [Fact]
    public void ReturnsEmptyListWhenNoMatchingKeysExist()
    {
        var instance = CreateInstance("Task1");
        var texts = CreateTexts(
            ("nb", new Dictionary<string, string>()));

        var localizations = ApplicationTextParser.GetLocalizationsFromApplicationTexts(
            "title",
            instance,
            texts,
            InstanceDerivedStatus.AwaitingSignature);

        Assert.Empty(localizations);
    }

    [Fact]
    public void ResolvesEachLanguageIndependently()
    {
        var instance = CreateInstance("Task1");
        var texts = CreateTexts(
            ("nb", new Dictionary<string, string>
            {
                { "dp.summary._any_.awaitingsignature", "nb-any" }
            }),
            ("nn", new Dictionary<string, string>
            {
                { "dp.summary", "nn-fallback" }
            }));

        var localizations = ApplicationTextParser.GetLocalizationsFromApplicationTexts(
            "summary",
            instance,
            texts,
            InstanceDerivedStatus.AwaitingSignature);

        Assert.Collection(
            localizations,
            nb =>
            {
                Assert.Equal("nb", nb.LanguageCode);
                Assert.Equal("nb-any", nb.Value);
            },
            nn =>
            {
                Assert.Equal("nn", nn.LanguageCode);
                Assert.Equal("nn-fallback", nn.Value);
            });
    }

    [Fact]
    public void FiltersOutInvalidLanguageCodes()
    {
        var instance = CreateInstance(null);
        var texts = CreateTexts(
            ("nb", new Dictionary<string, string>
            {
                { "dp.summary", "valid" }
            }),
            ("xx", new Dictionary<string, string>
            {
                { "dp.summary", "invalid" }
            }));

        var localizations = ApplicationTextParser.GetLocalizationsFromApplicationTexts(
            "summary",
            instance,
            texts,
            InstanceDerivedStatus.AwaitingSignature);

        Assert.Collection(localizations, loc =>
        {
            Assert.Equal("nb", loc.LanguageCode);
            Assert.Equal("valid", loc.Value);
        });
    }

    private static Instance CreateInstance(string? taskId)
    {
        var instance = new Instance();
        if (taskId is null)
        {
            return instance;
        }

        instance.Process = new ProcessState
        {
            CurrentTask = new ProcessElementInfo
            {
                ElementId = taskId
            }
        };

        return instance;
    }

    private static ApplicationTexts CreateTexts(params (string language, Dictionary<string, string> texts)[] translations)
    {
        var storageTranslations = new Dictionary<string, ApplicationTextsTranslation>(translations.Length);
        foreach (var (language, textsDictionary) in translations)
        {
            storageTranslations.Add(language, new ApplicationTextsTranslation
            {
                Language = language,
                Texts = textsDictionary
            });
        }

        return new ApplicationTexts
        {
            Translations = storageTranslations
        };
    }
}

using System;
using Altinn.ApiClients.Maskinporten.Config;
using Altinn.DialogportenAdapter.WebApi;
using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Enums;
using Altinn.Platform.Storage.Interface.Models;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Altinn.DialogportenAdapter.Unit.Tests.Features.Command.Sync;

public class StorageDialogportenDataMergerTest
{
    private readonly IRegisterRepository _registerRepositoryMock = Substitute.For<IRegisterRepository>();
    private readonly StorageDialogportenDataMerger _storageDialogportenDataMerger;
    private const int UserIdWithDisplayName = 1;
    private const int UserIdWithUnknownUrn = 2;
    private const int UserUnknown = 999;
    private AdapterFeatureFlagSettings _featureFlags = new() { EnableSubmissionTransmissions = true };

    public StorageDialogportenDataMergerTest()
    {
        var options = Substitute.For<IOptionsSnapshot<Settings>>();
        options.Value.Returns(new Settings
        {
            DialogportenAdapter = new DialogportenAdapterSettings(
                Maskinporten: new MaskinportenSettings
                {
                },
                Altinn: new AltinnPlatformSettings
                (
                    BaseUri: new Uri("http://altinn.localhost/"),
                    InternalStorageEndpoint: new Uri("http://altinn.storage.localhost/"),
                    InternalRegisterEndpoint: new Uri("http://altinn.register.localhost/"),
                    SubscriptionKey: "subscriptionKey"
                ),
                Dialogporten: new DialogportenSettings(BaseUri: new Uri("http://dialogporten.localhost/")),
                Adapter: new AdapterSettings(
                    BaseUri: new Uri("http://adapter.localhost/"),
                    FeatureFlag: _featureFlags
                ),
                Authentication: new AuthenticationSettings(JwtBearerWellKnown: "http://well.known.localhost")
            ),
            WolverineSettings = new WolverineSettings("http://service.bus.localhost", 0)
        });

        _registerRepositoryMock.GetActorUrnByPartyId(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { { "partyId", "urn:actor.by.party.id" } });

        _registerRepositoryMock.GetActorUrnByUserId(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>
            {
                { UserIdWithDisplayName.ToString(), "urn:altinn:displayName:Leif" },
                { UserIdWithUnknownUrn.ToString(), "urn:altinn:name:Leif" },
            });


        _storageDialogportenDataMerger = new StorageDialogportenDataMerger(
            options,
            new ActivityDtoTransformer(_registerRepositoryMock),
            _registerRepositoryMock
        );
    }

    [Fact(DisplayName = "Given a minimal MergeDto, should return a DialogDto")]
    public async Task Merge_MinimalMergeDto_ReturnsExpectedDialogDto()
    {
        var mergeDto = new MergeDto(
            Application: new Application
            {
                Title = new Dictionary<string, string> { { "nb", "title" } },
            },
            ApplicationTexts: new ApplicationTexts
            {
                Translations = new Dictionary<string, ApplicationTextsTranslation>()
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents =
                [
                    new InstanceEvent
                    {
                        User = new PlatformUser
                        {
                            UserId = 1,
                            OrgId = "org",
                        }
                    }
                ]
            },
            Instance: new Instance
            {
                Created = new DateTime(2000, 1, 1, 1, 1, 1),
                CreatedBy = "Me",
                LastChanged = new DateTime(2000, 1, 1, 1, 1, 2),
                LastChangedBy = "AlsoMe",
                Id = "id",
                InstanceOwner = new InstanceOwner
                {
                    PartyId = "partyId",
                },
                AppId = "appid",
                Org = "org",
                SelfLinks = null,
                DueBefore = null,
                VisibleAfter = null,
                Process = new ProcessState(),
                Status = new InstanceStatus(),
                CompleteConfirmations = [],
                Data = [],
                PresentationTexts = new Dictionary<string, string>(),
                DataValues = new Dictionary<string, string>()
            },
            ExistingDialog: null,
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_appid",
            Party = "urn:actor.by.party.id",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = null,
            DueAt = null,
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(2000, 1, 1, 1, 1, 1),
            UpdatedAt = new DateTime(2000, 1, 1, 1, 1, 2),
            Status = DialogStatus.InProgress,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = new ServiceOwnerContext
            {
                ServiceOwnerLabels =
                [
                    new ServiceOwnerLabel { Value = "urn:altinn:integration:storage:id" }
                ]
            },
            Content = new ContentDto
            {
                Summary = new ContentValueDto
                {
                    Value =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Innsendingen er klar for å fylles ut." },
                        new LocalizationDto { LanguageCode = "nn", Value = "Innsendinga er klar til å fyllast ut." },
                        new LocalizationDto { LanguageCode = "en", Value = "The submission is ready to be filled out." }
                    ],
                    MediaType = "text/plain"
                },
                Title = new ContentValueDto
                {
                    Value = [new LocalizationDto { Value = "title", LanguageCode = "nb" }],
                    MediaType = "text/plain",
                },
                ExtendedStatus = null,
            },
            SearchTags = [],
            Attachments = [],
            Transmissions = [],
            GuiActions =
            [
                new GuiActionDto
                {
                    Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.Delete),
                    Action = "delete",
                    Url = "http://adapter.localhost/api/v1/instance/id",
                    AuthorizationAttribute = null,
                    IsDeleteDialogAction = true,
                    HttpMethod = HttpVerb.DELETE,
                    Priority = DialogGuiActionPriority.Secondary,
                    Title =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Slett" },
                        new LocalizationDto { LanguageCode = "nn", Value = "Slett" },
                        new LocalizationDto { LanguageCode = "en", Value = "Delete" }
                    ],
                    Prompt = null
                },
                new GuiActionDto
                {
                    Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.GoTo),
                    Action = "write",
                    Url =
                        "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Forg.apps.altinn.localhost%2Fappid%2F%3FdontChooseReportee%3Dtrue%23%2Finstance%2Fid",
                    AuthorizationAttribute = null,
                    IsDeleteDialogAction = false,
                    HttpMethod = HttpVerb.GET,
                    Priority = DialogGuiActionPriority.Primary,
                    Title =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Gå til skjemautfylling" },
                        new LocalizationDto { LanguageCode = "nn", Value = "Gå til skjemautfylling" },
                        new LocalizationDto { LanguageCode = "en", Value = "Go to form completion" }
                    ],
                    Prompt = null
                }
            ],
            ApiActions = [],
            Activities = [],
            Deleted = false
        });
    }

    [Fact(DisplayName = "Given a localized Substatus, Substatus should be mapped to ExtendedStatus in all languages")]
    public async Task Merge_LocalizedSubstatus_MapsAllLanguagesToExtendedStatus()
    {
        var mergeDto = new MergeDto(
            Application: new Application
            {
                Title = new Dictionary<string, string> { { "nb", "title" } },
            },
            ApplicationTexts: new ApplicationTexts
            {
                Translations = new Dictionary<string, ApplicationTextsTranslation>()
                {
                    {
                        "nb",
                        new ApplicationTextsTranslation
                        {
                            Language = "nb", Texts = new Dictionary<string, string>
                            {
                                { "substatus.label", "Registrering av tiltak" },
                                {
                                    "substatus.description",
                                    "øke sikkerheten og beredskapen i den digitale grunnmuren i sårbare kommuner og regioner gjennom målrettede tilskudd"
                                },
                            }
                        }
                    },
                    {
                        "nn",
                        new ApplicationTextsTranslation
                        {
                            Language = "nn", Texts = new Dictionary<string, string>
                            {
                                { "substatus.label", "Registrering av tiltak (nn)" },
                                {
                                    "substatus.description",
                                    "Auke tryggleiken og beredskapen i den digitale grunnmuren i sårbare kommunar og regionar gjennom målretta tilskot"
                                },
                            }
                        }
                    },
                    {
                        "en",
                        new ApplicationTextsTranslation
                        {
                            Language = "en", Texts = new Dictionary<string, string>
                            {
                                { "substatus.label", "Registration of measures" },
                                {
                                    "substatus.description",
                                    "Increase security and preparedness in the digital infrastructure in vulnerable municipalities and regions through targeted grants"
                                },
                            }
                        }
                    },
                }
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents =
                [
                    new InstanceEvent
                    {
                        User = new PlatformUser
                        {
                            UserId = 1,
                            OrgId = "org",
                        }
                    }
                ]
            },
            Instance: new Instance
            {
                Created = new DateTime(2000, 1, 1, 1, 1, 1),
                CreatedBy = "Me",
                LastChanged = new DateTime(2000, 1, 1, 1, 1, 2),
                LastChangedBy = "Me",
                Id = "id",
                InstanceOwner = new InstanceOwner
                {
                    PartyId = "partyId",
                },
                AppId = "appid",
                Org = "org",
                SelfLinks = null,
                DueBefore = null,
                VisibleAfter = null,
                Process = new ProcessState(),
                Status = new InstanceStatus
                {
                    Substatus = new Substatus
                    {
                        Label = "substatus.label",
                    }
                },
                CompleteConfirmations = [],
                Data = [],
                PresentationTexts = new Dictionary<string, string>(),
                DataValues = new Dictionary<string, string>()
            },
            ExistingDialog: null,
            IsMigration: false);

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_appid",
            Party = "urn:actor.by.party.id",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = null,
            DueAt = null,
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(2000, 1, 1, 1, 1, 1),
            UpdatedAt = new DateTime(2000, 1, 1, 1, 1, 2),
            Status = DialogStatus.InProgress,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = new ServiceOwnerContext
            {
                ServiceOwnerLabels =
                [
                    new ServiceOwnerLabel { Value = "urn:altinn:integration:storage:id" }
                ]
            },
            Content = new ContentDto
            {
                Summary = new ContentValueDto
                {
                    Value =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Innsendingen er klar for å fylles ut." },
                        new LocalizationDto { LanguageCode = "nn", Value = "Innsendinga er klar til å fyllast ut." },
                        new LocalizationDto { LanguageCode = "en", Value = "The submission is ready to be filled out." }
                    ],
                    MediaType = "text/plain"
                },
                Title = new ContentValueDto
                {
                    Value = [new LocalizationDto { Value = "title", LanguageCode = "nb" }],
                    MediaType = "text/plain",
                },
                ExtendedStatus = new ContentValueDto
                {
                    Value =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Registrering av tiltak" },
                        new LocalizationDto { LanguageCode = "nn", Value = "Registrering av tiltak (nn)" },
                        new LocalizationDto { LanguageCode = "en", Value = "Registration of measures" }
                    ],
                    MediaType = "text/plain"
                },
            },
            SearchTags = [],
            Attachments = [],
            Transmissions = [],
            GuiActions =
            [
                new GuiActionDto
                {
                    Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.Delete),
                    Action = "delete",
                    Url = "http://adapter.localhost/api/v1/instance/id",
                    AuthorizationAttribute = null,
                    IsDeleteDialogAction = true,
                    HttpMethod = HttpVerb.DELETE,
                    Priority = DialogGuiActionPriority.Secondary,
                    Title =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Slett" },
                        new LocalizationDto { LanguageCode = "nn", Value = "Slett" },
                        new LocalizationDto { LanguageCode = "en", Value = "Delete" }
                    ],
                    Prompt = null
                },
                new GuiActionDto
                {
                    Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.GoTo),
                    Action = "write",
                    Url =
                        "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Forg.apps.altinn.localhost%2Fappid%2F%3FdontChooseReportee%3Dtrue%23%2Finstance%2Fid",
                    AuthorizationAttribute = null,
                    IsDeleteDialogAction = false,
                    HttpMethod = HttpVerb.GET,
                    Priority = DialogGuiActionPriority.Primary,
                    Title =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Gå til skjemautfylling" },
                        new LocalizationDto { LanguageCode = "nn", Value = "Gå til skjemautfylling" },
                        new LocalizationDto { LanguageCode = "en", Value = "Go to form completion" }
                    ],
                    Prompt = null
                }
            ],
            ApiActions = [],
            Activities = [],
            Deleted = false
        });
    }

    [Fact(DisplayName = "Given a non-localized Substatus, Substatus should be mapped to Extendedstatus in NB")]
    public async Task Merge_SubstatusWithText_AssumesNorwegian()
    {
        var mergeDto = new MergeDto(
            Application: new Application
            {
                Title = new Dictionary<string, string> { { "nb", "title" } },
            },
            ApplicationTexts: new ApplicationTexts
            {
                Translations = new Dictionary<string, ApplicationTextsTranslation>()
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents =
                [
                    new InstanceEvent
                    {
                        User = new PlatformUser
                        {
                            UserId = 1,
                            OrgId = "org",
                        }
                    }
                ]
            },
            Instance: new Instance
            {
                Created = new DateTime(2000, 1, 1, 1, 1, 1),
                CreatedBy = "Me",
                LastChanged = new DateTime(2000, 1, 1, 1, 1, 2),
                LastChangedBy = "Me",
                Id = "id",
                InstanceOwner = new InstanceOwner
                {
                    PartyId = "partyId",
                },
                AppId = "appid",
                Org = "org",
                SelfLinks = null,
                DueBefore = null,
                VisibleAfter = null,
                Process = new ProcessState(),
                Status = new InstanceStatus
                {
                    Substatus = new Substatus
                    {
                        Label = "En substatus som vi antar er på norsk",
                    }
                },
                CompleteConfirmations = [],
                Data = [],
                PresentationTexts = new Dictionary<string, string>(),
                DataValues = new Dictionary<string, string>()
            },
            ExistingDialog: null,
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_appid",
            Party = "urn:actor.by.party.id",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = null,
            DueAt = null,
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(2000, 1, 1, 1, 1, 1),
            UpdatedAt = new DateTime(2000, 1, 1, 1, 1, 2),
            Status = DialogStatus.InProgress,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = new ServiceOwnerContext
            {
                ServiceOwnerLabels =
                [
                    new ServiceOwnerLabel { Value = "urn:altinn:integration:storage:id" }
                ]
            },
            Content = new ContentDto
            {
                Summary = new ContentValueDto
                {
                    Value =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Innsendingen er klar for å fylles ut." },
                        new LocalizationDto { LanguageCode = "nn", Value = "Innsendinga er klar til å fyllast ut." },
                        new LocalizationDto { LanguageCode = "en", Value = "The submission is ready to be filled out." }
                    ],
                    MediaType = "text/plain"
                },
                Title = new ContentValueDto
                {
                    Value = [new LocalizationDto { Value = "title", LanguageCode = "nb" }],
                    MediaType = "text/plain",
                },
                ExtendedStatus = new ContentValueDto
                {
                    Value =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "En substatus som vi antar er på norsk" },
                    ],
                    MediaType = "text/plain"
                },
            },
            SearchTags = [],
            Attachments = [],
            Transmissions = [],
            GuiActions =
            [
                new GuiActionDto
                {
                    Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.Delete),
                    Action = "delete",
                    Url = "http://adapter.localhost/api/v1/instance/id",
                    AuthorizationAttribute = null,
                    IsDeleteDialogAction = true,
                    HttpMethod = HttpVerb.DELETE,
                    Priority = DialogGuiActionPriority.Secondary,
                    Title =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Slett" },
                        new LocalizationDto { LanguageCode = "nn", Value = "Slett" },
                        new LocalizationDto { LanguageCode = "en", Value = "Delete" }
                    ],
                    Prompt = null
                },
                new GuiActionDto
                {
                    Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.GoTo),
                    Action = "write",
                    Url =
                        "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Forg.apps.altinn.localhost%2Fappid%2F%3FdontChooseReportee%3Dtrue%23%2Finstance%2Fid",
                    AuthorizationAttribute = null,
                    IsDeleteDialogAction = false,
                    HttpMethod = HttpVerb.GET,
                    Priority = DialogGuiActionPriority.Primary,
                    Title =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Gå til skjemautfylling" },
                        new LocalizationDto { LanguageCode = "nn", Value = "Gå til skjemautfylling" },
                        new LocalizationDto { LanguageCode = "en", Value = "Go to form completion" }
                    ],
                    Prompt = null
                }
            ],
            ApiActions = [],
            Activities = [],
            Deleted = false
        });
    }

    [Fact(DisplayName = "Given DueBefore and VisibleAfter in the past, set both to null")]
    public async Task Merge_DueBeforeAndVisibleAfterInThePast_SetBothToNull()
    {
        var mergeDto = new MergeDto(
            Application: new Application
            {
                Title = new Dictionary<string, string> { { "nb", "title" } },
            },
            ApplicationTexts: new ApplicationTexts
            {
                Translations = new Dictionary<string, ApplicationTextsTranslation>()
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents =
                [
                    new InstanceEvent
                    {
                        User = new PlatformUser
                        {
                            UserId = 1,
                            OrgId = "org",
                        }
                    }
                ]
            },
            Instance: new Instance
            {
                Created = new DateTime(2000, 1, 1, 1, 1, 1),
                CreatedBy = "Me",
                LastChanged = new DateTime(2000, 1, 1, 1, 1, 2),
                LastChangedBy = "Me",
                Id = "id",
                InstanceOwner = new InstanceOwner
                {
                    PartyId = "partyId",
                },
                AppId = "appid",
                Org = "org",
                SelfLinks = null,
                DueBefore = new DateTime(2000, 1, 1, 1, 1, 3),
                VisibleAfter = new DateTime(2000, 1, 1, 1, 1, 4),
                Process = new ProcessState(),
                Status = new InstanceStatus(),
                CompleteConfirmations = [],
                Data = [],
                PresentationTexts = new Dictionary<string, string>(),
                DataValues = new Dictionary<string, string>()
            },
            ExistingDialog: null,
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_appid",
            Party = "urn:actor.by.party.id",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = null,
            DueAt = null,
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(2000, 1, 1, 1, 1, 1),
            UpdatedAt = new DateTime(2000, 1, 1, 1, 1, 2),
            Status = DialogStatus.InProgress,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = new ServiceOwnerContext
            {
                ServiceOwnerLabels =
                [
                    new ServiceOwnerLabel { Value = "urn:altinn:integration:storage:id" }
                ]
            },
            Content = new ContentDto
            {
                Summary = new ContentValueDto
                {
                    Value =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Innsendingen er klar for å fylles ut." },
                        new LocalizationDto { LanguageCode = "nn", Value = "Innsendinga er klar til å fyllast ut." },
                        new LocalizationDto { LanguageCode = "en", Value = "The submission is ready to be filled out." }
                    ],
                    MediaType = "text/plain"
                },
                Title = new ContentValueDto
                {
                    Value = [new LocalizationDto { Value = "title", LanguageCode = "nb" }],
                    MediaType = "text/plain",
                },
                ExtendedStatus = null
            },
            SearchTags = [],
            Attachments = [],
            Transmissions = [],
            GuiActions =
            [
                new GuiActionDto
                {
                    Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.Delete),
                    Action = "delete",
                    Url = "http://adapter.localhost/api/v1/instance/id",
                    AuthorizationAttribute = null,
                    IsDeleteDialogAction = true,
                    HttpMethod = HttpVerb.DELETE,
                    Priority = DialogGuiActionPriority.Secondary,
                    Title =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Slett" },
                        new LocalizationDto { LanguageCode = "nn", Value = "Slett" },
                        new LocalizationDto { LanguageCode = "en", Value = "Delete" }
                    ],
                    Prompt = null
                },
                new GuiActionDto
                {
                    Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.GoTo),
                    Action = "write",
                    Url =
                        "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Forg.apps.altinn.localhost%2Fappid%2F%3FdontChooseReportee%3Dtrue%23%2Finstance%2Fid",
                    AuthorizationAttribute = null,
                    IsDeleteDialogAction = false,
                    HttpMethod = HttpVerb.GET,
                    Priority = DialogGuiActionPriority.Primary,
                    Title =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Gå til skjemautfylling" },
                        new LocalizationDto { LanguageCode = "nn", Value = "Gå til skjemautfylling" },
                        new LocalizationDto { LanguageCode = "en", Value = "Go to form completion" }
                    ],
                    Prompt = null
                }
            ],
            ApiActions = [],
            Activities = [],
            Deleted = false
        });
    }

    [Fact(DisplayName = "Given DueBefore and VisibleAfter in the future, MergeDto should include both fields")]
    public async Task Merge_DueBeforeAndVisibleAfterInTheFuture_IncludesBoth()
    {
        var mergeDto = new MergeDto(
            Application: new Application
            {
                Title = new Dictionary<string, string> { { "nb", "title" } },
            },
            ApplicationTexts: new ApplicationTexts
            {
                Translations = new Dictionary<string, ApplicationTextsTranslation>()
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents =
                [
                    new InstanceEvent
                    {
                        User = new PlatformUser
                        {
                            UserId = 1,
                            OrgId = "org",
                        }
                    },
                ]
            },
            Instance: new Instance
            {
                Created = new DateTime(2000, 1, 1, 1, 1, 1),
                CreatedBy = "Me",
                LastChanged = new DateTime(2000, 1, 1, 1, 1, 2),
                LastChangedBy = "Me",
                Id = "id",
                InstanceOwner = new InstanceOwner
                {
                    PartyId = "partyId",
                },
                AppId = "appid",
                Org = "org",
                SelfLinks = null,
                DueBefore = new DateTime(9999, 1, 1, 1, 1, 3),
                VisibleAfter = new DateTime(9999, 1, 1, 1, 1, 4),
                Process = new ProcessState(),
                Status = new InstanceStatus(),
                CompleteConfirmations = [],
                Data = [],
                PresentationTexts = new Dictionary<string, string>(),
                DataValues = new Dictionary<string, string>()
            },
            ExistingDialog: null,
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_appid",
            Party = "urn:actor.by.party.id",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = new DateTime(9999, 1, 1, 1, 1, 4),
            DueAt = new DateTime(9999, 1, 1, 1, 1, 3),
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(2000, 1, 1, 1, 1, 1),
            UpdatedAt = new DateTime(2000, 1, 1, 1, 1, 2),
            Status = DialogStatus.InProgress,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = new ServiceOwnerContext
            {
                ServiceOwnerLabels =
                [
                    new ServiceOwnerLabel { Value = "urn:altinn:integration:storage:id" }
                ]
            },
            Content = new ContentDto
            {
                Summary = new ContentValueDto
                {
                    Value =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Innsendingen er klar for å fylles ut." },
                        new LocalizationDto { LanguageCode = "nn", Value = "Innsendinga er klar til å fyllast ut." },
                        new LocalizationDto { LanguageCode = "en", Value = "The submission is ready to be filled out." }
                    ],
                    MediaType = "text/plain"
                },
                Title = new ContentValueDto
                {
                    Value = [new LocalizationDto { Value = "title", LanguageCode = "nb" }],
                    MediaType = "text/plain",
                },
                ExtendedStatus = null
            },
            SearchTags = [],
            Attachments = [],
            Transmissions = [],
            GuiActions =
            [
                new GuiActionDto
                {
                    Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.Delete),
                    Action = "delete",
                    Url = "http://adapter.localhost/api/v1/instance/id",
                    AuthorizationAttribute = null,
                    IsDeleteDialogAction = true,
                    HttpMethod = HttpVerb.DELETE,
                    Priority = DialogGuiActionPriority.Secondary,
                    Title =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Slett" },
                        new LocalizationDto { LanguageCode = "nn", Value = "Slett" },
                        new LocalizationDto { LanguageCode = "en", Value = "Delete" }
                    ],
                    Prompt = null
                },
                new GuiActionDto
                {
                    Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.GoTo),
                    Action = "write",
                    Url =
                        "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Forg.apps.altinn.localhost%2Fappid%2F%3FdontChooseReportee%3Dtrue%23%2Finstance%2Fid",
                    AuthorizationAttribute = null,
                    IsDeleteDialogAction = false,
                    HttpMethod = HttpVerb.GET,
                    Priority = DialogGuiActionPriority.Primary,
                    Title =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Gå til skjemautfylling" },
                        new LocalizationDto { LanguageCode = "nn", Value = "Gå til skjemautfylling" },
                        new LocalizationDto { LanguageCode = "en", Value = "Go to form completion" }
                    ],
                    Prompt = null
                }
            ],
            ApiActions = [],
            Activities = [],
            Deleted = false
        });
    }

    [Fact(DisplayName = "Given InstanceEvents of all types, InstanceEvents are mapped to Activities")]
    public async Task Merge_WithAllTypesOfInstanceEvents_MergesIntoActivitiesAsExpected()
    {
        var mergeDto = new MergeDto(
            Application: new Application
            {
                Title = new Dictionary<string, string> { { "nb", "title" } },
                MessageBoxConfig = new MessageBoxConfig
                {
                    SyncAdapterSettings = new SyncAdapterSettings { DisableAddTransmissions = true }
                },
            },
            ApplicationTexts: new ApplicationTexts
            {
                Translations = new Dictionary<string, ApplicationTextsTranslation>()
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents =
                [
                    new InstanceEvent
                    {
                        Id = Guid.Parse("019bc71b-eaf7-7fdf-a7ec-f85248d2293c"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 1),
                        EventType = nameof(InstanceEventType.Created),
                        User = new PlatformUser
                        {
                            UserId = UserIdWithDisplayName,
                        },
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("4ef9d179-2d4b-403f-9fcc-6f7cd619f12e"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 2),
                        EventType = nameof(InstanceEventType.Deleted),
                        User = new PlatformUser
                        {
                            UserId = UserIdWithUnknownUrn,
                        }
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("b26a5762-a719-48fc-aa11-6b89c409264b"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 3),
                        EventType = nameof(InstanceEventType.Undeleted),
                        User = new PlatformUser
                        {
                            UserId = UserUnknown,
                            EndUserSystemId = 3
                        }
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("79a36e66-dc26-4f92-a462-0522af8f17d5"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 4),
                        EventType = nameof(InstanceEventType.SentToSign),
                        User = new PlatformUser
                        {
                            UserId = UserUnknown,
                            SystemUserOwnerOrgNo = "123456789"
                        }
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("f880f2d0-2b70-4875-8547-e747144f5952"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 5),
                        EventType = nameof(InstanceEventType.Signed),
                        User = new PlatformUser
                        {
                            UserId = UserUnknown,
                            OrgId = "org",
                        }
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("5a0fd83c-ac18-47fd-bc9b-c5efc2e93d61"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 6),
                        EventType = nameof(InstanceEventType.SentToPayment),
                        User = new PlatformUser
                        {
                            UserId = 1,
                        }
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("5e9e8c0f-c417-4d0a-aa34-c19bb45c358b"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 7),
                        EventType = nameof(InstanceEventType.SentToFormFill),
                        User = new PlatformUser
                        {
                            UserId = 1,
                        }
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("001a6e3c-5046-4e41-a83d-b5a3ed22afb1"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 8),
                        EventType = nameof(InstanceEventType.SentToSendIn),
                        User = new PlatformUser
                        {
                            UserId = 1,
                        }
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("6c5532fd-42b8-4fe6-aebe-62ef9e035791"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 9),
                        EventType = nameof(InstanceEventType.Submited),
                        User = new PlatformUser
                        {
                            UserId = 1,
                        }
                    },
                ]
            },
            Instance: new Instance
            {
                Created = new DateTime(2000, 1, 1, 1, 1, 1),
                CreatedBy = "Me",
                LastChanged = new DateTime(2000, 1, 1, 1, 1, 2),
                LastChangedBy = "Me",
                Id = "id",
                InstanceOwner = new InstanceOwner
                {
                    PartyId = "partyId",
                },
                AppId = "appid",
                Org = "org",
                SelfLinks = null,
                DueBefore = null,
                VisibleAfter = null,
                Process = new ProcessState(),
                Status = new InstanceStatus(),
                CompleteConfirmations = [],
                Data = [],
                PresentationTexts = new Dictionary<string, string>(),
                DataValues = new Dictionary<string, string>()
            },
            ExistingDialog: null,
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        using (new AssertionScope { FormattingOptions = { MaxLines = 500 } })
        {
            actualDialogDto.Should().BeEquivalentTo(new DialogDto
            {
                Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
                IsApiOnly = false,
                Revision = null,
                ServiceResource = "urn:altinn:resource:app_appid",
                Party = "urn:actor.by.party.id",
                Progress = null,
                ExtendedStatus = null,
                ExternalReference = null,
                VisibleFrom = null,
                DueAt = null,
                Process = null,
                PrecedingProcess = null,
                ExpiresAt = null,
                CreatedAt = new DateTime(2000, 1, 1, 1, 1, 1),
                UpdatedAt = new DateTime(2000, 1, 1, 1, 1, 2),
                Status = DialogStatus.Draft,
                SystemLabel = SystemLabel.Default,
                ServiceOwnerContext = new ServiceOwnerContext
                {
                    ServiceOwnerLabels =
                    [
                        new ServiceOwnerLabel { Value = "urn:altinn:integration:storage:id" }
                    ]
                },
                Content = new ContentDto
                {
                    Summary = new ContentValueDto
                    {
                        Value =
                        [
                            new LocalizationDto
                                { LanguageCode = "nb", Value = "Innsendingen er klar for å fylles ut." },
                            new LocalizationDto
                                { LanguageCode = "nn", Value = "Innsendinga er klar til å fyllast ut." },
                            new LocalizationDto
                                { LanguageCode = "en", Value = "The submission is ready to be filled out." }
                        ],
                        MediaType = "text/plain"
                    },
                    Title = new ContentValueDto
                    {
                        Value = [new LocalizationDto { Value = "title", LanguageCode = "nb" }],
                        MediaType = "text/plain",
                    },
                    ExtendedStatus = null
                },
                SearchTags = [],
                Attachments = [],
                GuiActions =
                [
                    new GuiActionDto
                    {
                        Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.Delete),
                        Action = "delete",
                        Url = "http://adapter.localhost/api/v1/instance/id",
                        AuthorizationAttribute = null,
                        IsDeleteDialogAction = true,
                        HttpMethod = HttpVerb.DELETE,
                        Priority = DialogGuiActionPriority.Secondary,
                        Title =
                        [
                            new LocalizationDto { LanguageCode = "nb", Value = "Slett" },
                            new LocalizationDto { LanguageCode = "nn", Value = "Slett" },
                            new LocalizationDto { LanguageCode = "en", Value = "Delete" }
                        ],
                        Prompt = null
                    },
                    new GuiActionDto
                    {
                        Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.GoTo),
                        Action = "write",
                        Url =
                            "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Forg.apps.altinn.localhost%2Fappid%2F%3FdontChooseReportee%3Dtrue%23%2Finstance%2Fid",
                        AuthorizationAttribute = null,
                        IsDeleteDialogAction = false,
                        HttpMethod = HttpVerb.GET,
                        Priority = DialogGuiActionPriority.Primary,
                        Title =
                        [
                            new LocalizationDto { LanguageCode = "nb", Value = "Gå til skjemautfylling" },
                            new LocalizationDto { LanguageCode = "nn", Value = "Gå til skjemautfylling" },
                            new LocalizationDto { LanguageCode = "en", Value = "Go to form completion" }
                        ],
                        Prompt = null
                    }
                ],
                ApiActions = [],
                Activities =
                [
                    new ActivityDto
                    {
                        Id = Guid.Parse("00e3c7a8-2248-7fdf-a7ec-f85248d2293c"),
                        CreatedAt = new DateTime(2001, 1, 1, 1, 1, 1),
                        ExtendedType = null,
                        Type = DialogActivityType.DialogCreated,
                        TransmissionId = null,
                        PerformedBy = new ActorDto
                        {
                            ActorType = ActorType.PartyRepresentative,
                            ActorName = "Leif",
                            ActorId = null
                        },
                        Description = []
                    },
                    new ActivityDto
                    {
                        Id = Guid.Parse("00e3c7a8-2630-703f-9fcc-6f7cd619f12e"),
                        CreatedAt = new DateTime(2001, 1, 1, 1, 1, 2),
                        ExtendedType = null,
                        Type = DialogActivityType.DialogDeleted,
                        TransmissionId = null,
                        PerformedBy = new ActorDto
                        {
                            ActorType = ActorType.PartyRepresentative,
                            ActorName = null,
                            ActorId = "urn:altinn:name:Leif"
                        },
                        Description = []
                    },
                    new ActivityDto
                    {
                        Id = Guid.Parse("00e3c7a8-2a18-78fc-aa11-6b89c409264b"),
                        CreatedAt = new DateTime(2001, 1, 1, 1, 1, 3),
                        ExtendedType = null,
                        Type = DialogActivityType.DialogRestored,
                        TransmissionId = null,
                        PerformedBy = new ActorDto
                        {
                            ActorType = ActorType.PartyRepresentative,
                            ActorName = "EUS #3",
                            ActorId = null
                        },
                        Description = []
                    },
                    new ActivityDto
                    {
                        Id = Guid.Parse("00e3c7a8-2e00-7f92-a462-0522af8f17d5"),
                        CreatedAt = new DateTime(2001, 1, 1, 1, 1, 4),
                        ExtendedType = null,
                        Type = DialogActivityType.SentToSigning,
                        TransmissionId = null,
                        PerformedBy = new ActorDto
                        {
                            ActorType = ActorType.PartyRepresentative,
                            ActorName = null,
                            ActorId = "urn:altinn:organization:identifier-no:123456789"
                        },
                        Description = []
                    },
                    new ActivityDto
                    {
                        Id = Guid.Parse("00e3c7a8-31e8-7875-8547-e747144f5952"),
                        CreatedAt = new DateTime(2001, 1, 1, 1, 1, 5),
                        ExtendedType = null,
                        Type = DialogActivityType.SignatureProvided,
                        TransmissionId = null,
                        PerformedBy = new ActorDto
                        {
                            ActorType = ActorType.ServiceOwner,
                            ActorName = null,
                            ActorId = null
                        },
                        Description = []
                    },
                    new ActivityDto
                    {
                        Id = Guid.Parse("00e3c7a8-35d0-77fd-bc9b-c5efc2e93d61"),
                        CreatedAt = new DateTime(2001, 1, 1, 1, 1, 6),
                        ExtendedType = null,
                        Type = DialogActivityType.SentToPayment,
                        TransmissionId = null,
                        PerformedBy = new ActorDto
                        {
                            ActorType = ActorType.PartyRepresentative,
                            ActorName = "Leif",
                            ActorId = null
                        },
                        Description = []
                    },
                    new ActivityDto
                    {
                        Id = Guid.Parse("00e3c7a8-39b8-7d0a-aa34-c19bb45c358b"),
                        CreatedAt = new DateTime(2001, 1, 1, 1, 1, 7),
                        ExtendedType = null,
                        Type = DialogActivityType.SentToFormFill,
                        TransmissionId = null,
                        PerformedBy = new ActorDto
                        {
                            ActorType = ActorType.PartyRepresentative,
                            ActorName = "Leif",
                            ActorId = null
                        },
                        Description = []
                    },
                    new ActivityDto
                    {
                        Id = Guid.Parse("00e3c7a8-3da0-7e41-a83d-b5a3ed22afb1"),
                        CreatedAt = new DateTime(2001, 1, 1, 1, 1, 8),
                        ExtendedType = null,
                        Type = DialogActivityType.SentToSendIn,
                        TransmissionId = null,
                        PerformedBy = new ActorDto
                        {
                            ActorType = ActorType.PartyRepresentative,
                            ActorName = "Leif",
                            ActorId = null
                        },
                        Description = []
                    },
                    new ActivityDto
                    {
                        Id = Guid.Parse("00e3c7a8-4188-7fe6-aebe-62ef9e035791"),
                        CreatedAt = new DateTime(2001, 1, 1, 1, 1, 9),
                        ExtendedType = null,
                        Type = DialogActivityType.FormSubmitted,
                        TransmissionId = null,
                        PerformedBy = new ActorDto
                        {
                            ActorType = ActorType.PartyRepresentative,
                            ActorName = "Leif",
                            ActorId = null
                        },
                        Description = []
                    },
                ],
                Deleted = false
            });
        }
    }

    [Fact(DisplayName =
        "Given DataElements (only attachments) with enabled Transmissions, DataElements are mapped into Attachments")]
    public async Task Merge_WithOnlyAttachmentsAndEnabledTransmissions_MergesIntoAttachmentsAsExpected()
    {
        var mergeDto = new MergeDto(
            Application: new Application
            {
                Title = new Dictionary<string, string> { { "nb", "title" } },
                DataTypes =
                [
                    new DataType
                    {
                        Id = null,
                        Description = new LanguageString { { "nb", "ikke i Gui: Uten ID" } },
                        AllowedContentTypes = null,
                        AllowedContributors = null,
                        AppLogic = null,
                        TaskId = null,
                        MaxSize = null,
                        MaxCount = 0,
                        MinCount = 0,
                        Grouping = null,
                        EnablePdfCreation = false,
                        EnableFileScan = false,
                        ValidationErrorOnPendingFileScan = false,
                        EnabledFileAnalysers = null,
                        EnabledFileValidators = null,
                        AllowedKeysForUserDefinedMetadata = null
                    },
                    new DataType
                    {
                        Id = "ref-data-as-pdf",
                        Description = new LanguageString { { "nb", "I gui: ID er ref-data-as-pdf" } },
                        AllowedContentTypes = null,
                        AllowedContributors = null,
                        AppLogic = null,
                        TaskId = null,
                        MaxSize = null,
                        MaxCount = 0,
                        MinCount = 0,
                        Grouping = null,
                        EnablePdfCreation = false,
                        EnableFileScan = false,
                        ValidationErrorOnPendingFileScan = false,
                        EnabledFileAnalysers = null,
                        EnabledFileValidators = null,
                        AllowedKeysForUserDefinedMetadata = null
                    },
                    new DataType
                    {
                        Id = "app-logic",
                        Description = new LanguageString { { "nb", "Ikke i Gui: AppLogic eksisterer" } },
                        AllowedContentTypes = null,
                        AllowedContributors = null,
                        AppLogic = new ApplicationLogic
                        {
                            AutoCreate = false,
                            ClassRef = "no.digdir.ClassRef",
                            SchemaRef = "123",
                            AllowAnonymousOnStateless = false,
                            AutoDeleteOnProcessEnd = false,
                            DisallowUserCreate = false,
                            DisallowUserDelete = false,
                            ShadowFields = null
                        },
                        TaskId = null,
                        MaxSize = null,
                        MaxCount = 0,
                        MinCount = 0,
                        Grouping = null,
                        EnablePdfCreation = false,
                        EnableFileScan = false,
                        ValidationErrorOnPendingFileScan = false,
                        EnabledFileAnalysers = null,
                        EnabledFileValidators = null,
                        AllowedKeysForUserDefinedMetadata = null
                    },
                    new DataType
                    {
                        Id = "app-owned",
                        Description = new LanguageString { { "nb", "Ikke i Gui: app:owned contributor" } },
                        AllowedContentTypes = null,
                        AllowedContributors = ["app:owned"],
                        AppLogic = null,
                        TaskId = null,
                        MaxSize = null,
                        MaxCount = 0,
                        MinCount = 0,
                        Grouping = null,
                        EnablePdfCreation = false,
                        EnableFileScan = false,
                        ValidationErrorOnPendingFileScan = false,
                        EnabledFileAnalysers = null,
                        EnabledFileValidators = null,
                        AllowedKeysForUserDefinedMetadata = null
                    },
                    new DataType
                    {
                        Id = "not-excluded",
                        Description = new LanguageString { { "nb", "I Gui: Matcher ingen regler" } },
                        AllowedContentTypes = null,
                        AllowedContributors = null,
                        AppLogic = null,
                        TaskId = null,
                        MaxSize = null,
                        MaxCount = 0,
                        MinCount = 0,
                        Grouping = null,
                        EnablePdfCreation = false,
                        EnableFileScan = false,
                        ValidationErrorOnPendingFileScan = false,
                        EnabledFileAnalysers = null,
                        EnabledFileValidators = null,
                        AllowedKeysForUserDefinedMetadata = null
                    },
                ]
            },
            ApplicationTexts: new ApplicationTexts
            {
                Translations = new Dictionary<string, ApplicationTextsTranslation>()
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents =
                [
                    new InstanceEvent
                    {
                        Id = Guid.Parse("019bc71b-eaf7-7fdf-a7ec-f85248d2293c"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 1),
                        EventType = nameof(InstanceEventType.Created),
                        User = new PlatformUser
                        {
                            UserId = UserIdWithDisplayName,
                        },
                    },
                ]
            },
            Instance: new Instance
            {
                Created = new DateTime(2000, 1, 1, 1, 1, 1),
                CreatedBy = "Me",
                LastChanged = new DateTime(2000, 1, 1, 1, 1, 2),
                LastChangedBy = "Me",
                Id = "id",
                InstanceOwner = new InstanceOwner
                {
                    PartyId = "partyId",
                },
                AppId = "appid",
                Org = "org",
                SelfLinks = null,
                DueBefore = null,
                VisibleAfter = null,
                Process = new ProcessState(),
                Status = new InstanceStatus(),
                CompleteConfirmations = [],
                Data =
                [
                    new DataElement
                    {
                        Created = new DateTime(2000, 1, 1, 1, 1, 1),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1),
                        LastChangedBy = "123456789",
                        Id = "019bd57e-ce5e-74ed-8130-3a1ac8af3d91",
                        InstanceGuid = "019bd57f-7146-73fa-9292-e6401d8ef5e8",
                        DataType = null,
                        Filename = "visible-because-of-missing-data-type",
                        ContentType = "application/pdf",
                        BlobStoragePath = "/pdfs",
                        SelfLinks = new ResourceLinks
                        {
                            Apps = null,
                            Platform = "http://platform.localhost"
                        },
                        Size = 1024,
                        ContentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                        Locked = false,
                        Refs = [Guid.Parse("019bd582-ef52-7850-a483-94ba72e9bba5")],
                        IsRead = false,
                        Tags = ["viktig", "konfidensiell"],
                        UserDefinedMetadata = [new KeyValueEntry { Key = "eier", Value = "Trond" }],
                        Metadata = [new KeyValueEntry { Key = "versjon", Value = "2" }],
                        DeleteStatus = null,
                        FileScanResult = FileScanResult.NotApplicable,
                        References =
                        [
                            new Reference
                            {
                                Value = "https://localhost",
                                Relation = RelationType.GeneratedFrom,
                                ValueType = ReferenceType.DataElement
                            }
                        ]
                    },
                    new DataElement
                    {
                        Created = new DateTime(2000, 1, 1, 1, 1, 1),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1),
                        LastChangedBy = "123456789",
                        Id = "019bd5eb-4239-7a40-a823-7735059ef136",
                        InstanceGuid = "019bd57f-7146-73fa-9292-e6401d8ef5e8",
                        DataType = "ref-data-as-pdf",
                        Filename = "visible-because-pdf-ref",
                        ContentType = "application/pdf",
                        BlobStoragePath = "/pdfs",
                        SelfLinks = new ResourceLinks
                        {
                            Apps = null,
                            Platform = "http://platform.localhost"
                        },
                        Size = 1024,
                        ContentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                        Locked = false,
                        Refs = [Guid.Parse("019bd582-ef52-7850-a483-94ba72e9bba5")],
                        IsRead = false,
                        Tags = ["viktig", "konfidensiell"],
                        UserDefinedMetadata = [new KeyValueEntry { Key = "eier", Value = "Trond" }],
                        Metadata = [new KeyValueEntry { Key = "versjon", Value = "2" }],
                        DeleteStatus = null,
                        FileScanResult = FileScanResult.NotApplicable,
                        References =
                        [
                            new Reference
                            {
                                Value = "https://localhost",
                                Relation = RelationType.GeneratedFrom,
                                ValueType = ReferenceType.DataElement
                            }
                        ]
                    },
                    new DataElement
                    {
                        Created = new DateTime(2000, 1, 1, 1, 1, 1),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1),
                        LastChangedBy = "123456789",
                        Id = "019bd5eb-62e2-711d-b79f-835d26cd1a58",
                        InstanceGuid = "019bd57f-7146-73fa-9292-e6401d8ef5e8",
                        DataType = "app-logic",
                        Filename = "not-visible-because-app-logic-exists",
                        ContentType = "application/pdf",
                        BlobStoragePath = "/pdfs",
                        SelfLinks = new ResourceLinks
                        {
                            Apps = null,
                            Platform = "http://platform.localhost"
                        },
                        Size = 1024,
                        ContentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                        Locked = false,
                        Refs = [Guid.Parse("019bd582-ef52-7850-a483-94ba72e9bba5")],
                        IsRead = false,
                        Tags = ["viktig", "konfidensiell"],
                        UserDefinedMetadata = [new KeyValueEntry { Key = "eier", Value = "Trond" }],
                        Metadata = [new KeyValueEntry { Key = "versjon", Value = "2" }],
                        DeleteStatus = null,
                        FileScanResult = FileScanResult.NotApplicable,
                        References =
                        [
                            new Reference
                            {
                                Value = "https://localhost",
                                Relation = RelationType.GeneratedFrom,
                                ValueType = ReferenceType.DataElement
                            }
                        ]
                    },
                    new DataElement
                    {
                        Created = new DateTime(2000, 1, 1, 1, 1, 1),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1),
                        LastChangedBy = "123456789",
                        Id = "019bd5eb-97ba-79a8-9f37-3ef8f82b2d0b",
                        InstanceGuid = "019bd57f-7146-73fa-9292-e6401d8ef5e8",
                        DataType = "app-owned",
                        Filename = "not-visible-because-app-owned",
                        ContentType = "application/pdf",
                        BlobStoragePath = "/pdfs",
                        SelfLinks = new ResourceLinks
                        {
                            Apps = null,
                            Platform = "http://platform.localhost"
                        },
                        Size = 1024,
                        ContentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                        Locked = false,
                        Refs = [Guid.Parse("019bd582-ef52-7850-a483-94ba72e9bba5")],
                        IsRead = false,
                        Tags = ["viktig", "konfidensiell"],
                        UserDefinedMetadata = [new KeyValueEntry { Key = "eier", Value = "Trond" }],
                        Metadata = [new KeyValueEntry { Key = "versjon", Value = "2" }],
                        DeleteStatus = null,
                        FileScanResult = FileScanResult.NotApplicable,
                        References =
                        [
                            new Reference
                            {
                                Value = "https://localhost",
                                Relation = RelationType.GeneratedFrom,
                                ValueType = ReferenceType.DataElement
                            }
                        ]
                    },
                    new DataElement
                    {
                        Created = new DateTime(2000, 1, 1, 1, 1, 1),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1),
                        LastChangedBy = "123456789",
                        Id = "019bd5eb-bd4f-7176-948e-79921affe066",
                        InstanceGuid = "019bd57f-7146-73fa-9292-e6401d8ef5e8",
                        DataType = "not-excluded",
                        Filename = "visible-not-excluded",
                        ContentType = "application/pdf",
                        BlobStoragePath = "/pdfs",
                        SelfLinks = new ResourceLinks
                        {
                            Apps = null,
                            Platform = "http://platform.localhost"
                        },
                        Size = 1024,
                        ContentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                        Locked = false,
                        Refs = [Guid.Parse("019bd582-ef52-7850-a483-94ba72e9bba5")],
                        IsRead = false,
                        Tags = ["viktig", "konfidensiell"],
                        UserDefinedMetadata = [new KeyValueEntry { Key = "eier", Value = "Trond" }],
                        Metadata = [new KeyValueEntry { Key = "versjon", Value = "2" }],
                        DeleteStatus = null,
                        FileScanResult = FileScanResult.NotApplicable,
                        References =
                        [
                            new Reference
                            {
                                Value = "https://localhost",
                                Relation = RelationType.GeneratedFrom,
                                ValueType = ReferenceType.DataElement
                            }
                        ]
                    },
                ],
                PresentationTexts = new Dictionary<string, string>(),
                DataValues = new Dictionary<string, string>()
            },
            ExistingDialog: null,
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        using (new AssertionScope { FormattingOptions = { MaxLines = 500 } })
        {
            actualDialogDto.Should().BeEquivalentTo(new DialogDto
            {
                Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
                IsApiOnly = false,
                Revision = null,
                ServiceResource = "urn:altinn:resource:app_appid",
                Party = "urn:actor.by.party.id",
                Progress = null,
                ExtendedStatus = null,
                ExternalReference = null,
                VisibleFrom = null,
                DueAt = null,
                Process = null,
                PrecedingProcess = null,
                ExpiresAt = null,
                CreatedAt = new DateTime(2000, 1, 1, 1, 1, 1),
                UpdatedAt = new DateTime(2000, 1, 1, 1, 1, 2),
                Status = DialogStatus.Draft,
                SystemLabel = SystemLabel.Default,
                ServiceOwnerContext = new ServiceOwnerContext
                {
                    ServiceOwnerLabels =
                    [
                        new ServiceOwnerLabel { Value = "urn:altinn:integration:storage:id" }
                    ]
                },
                Content = new ContentDto
                {
                    Summary = new ContentValueDto
                    {
                        Value =
                        [
                            new LocalizationDto
                                { LanguageCode = "nb", Value = "Innsendingen er klar for å fylles ut." },
                            new LocalizationDto
                                { LanguageCode = "nn", Value = "Innsendinga er klar til å fyllast ut." },
                            new LocalizationDto
                                { LanguageCode = "en", Value = "The submission is ready to be filled out." }
                        ],
                        MediaType = "text/plain"
                    },
                    Title = new ContentValueDto
                    {
                        Value = [new LocalizationDto { Value = "title", LanguageCode = "nb" }],
                        MediaType = "text/plain",
                    },
                    ExtendedStatus = null
                },
                SearchTags = [],
                Attachments =
                [
                    new AttachmentDto
                    {
                        Id = Guid.Parse("00dc6ad0-9a48-74ed-8130-3a1ac8af3d91"),
                        DisplayName =
                        [
                            new LocalizationDto
                            {
                                Value = "visible-because-of-missing-data-type",
                                LanguageCode = "nb"
                            }
                        ],
                        Urls =
                        [
                            new AttachmentUrlDto
                            {
                                Id = Guid.Parse("00dc6ad0-9a48-74ed-8130-3a1ac8af3d91"),
                                Url =
                                    "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Fplatform.localhost%3FdontChooseReportee%3Dtrue",
                                MediaType = "application/pdf",
                                ConsumerType = AttachmentUrlConsumerType.Gui
                            }
                        ]
                    },
                    new AttachmentDto
                    {
                        Id = Guid.Parse("00dc6ad0-9a48-7a40-a823-7735059ef136"),
                        DisplayName =
                        [
                            new LocalizationDto
                            {
                                Value = "visible-because-pdf-ref",
                                LanguageCode = "nb"
                            }
                        ],
                        Urls =
                        [
                            new AttachmentUrlDto
                            {
                                Id = Guid.Parse("00dc6ad0-9a48-7a40-a823-7735059ef136"),
                                Url =
                                    "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Fplatform.localhost%3FdontChooseReportee%3Dtrue",
                                MediaType = "application/pdf",
                                ConsumerType = AttachmentUrlConsumerType.Gui
                            }
                        ]
                    },
                    new AttachmentDto
                    {
                        Id = Guid.Parse("00dc6ad0-9a48-711d-b79f-835d26cd1a58"),
                        DisplayName =
                        [
                            new LocalizationDto
                            {
                                Value = "not-visible-because-app-logic-exists",
                                LanguageCode = "nb"
                            }
                        ],
                        Urls =
                        [
                            new AttachmentUrlDto
                            {
                                Id = Guid.Parse("00dc6ad0-9a48-711d-b79f-835d26cd1a58"),
                                Url = "http://platform.localhost",
                                MediaType = "application/pdf",
                                ConsumerType = AttachmentUrlConsumerType.Api
                            }
                        ]
                    },
                    new AttachmentDto
                    {
                        Id = Guid.Parse("00dc6ad0-9a48-79a8-9f37-3ef8f82b2d0b"),
                        DisplayName =
                        [
                            new LocalizationDto
                            {
                                Value = "not-visible-because-app-owned",
                                LanguageCode = "nb"
                            }
                        ],
                        Urls =
                        [
                            new AttachmentUrlDto
                            {
                                Id = Guid.Parse("00dc6ad0-9a48-79a8-9f37-3ef8f82b2d0b"),
                                Url = "http://platform.localhost",
                                MediaType = "application/pdf",
                                ConsumerType = AttachmentUrlConsumerType.Api
                            }
                        ]
                    },
                    new AttachmentDto
                    {
                        Id = Guid.Parse("00dc6ad0-9a48-7176-948e-79921affe066"),
                        DisplayName =
                        [
                            new LocalizationDto
                            {
                                Value = "visible-not-excluded",
                                LanguageCode = "nb"
                            }
                        ],
                        Urls =
                        [
                            new AttachmentUrlDto
                            {
                                Id = Guid.Parse("00dc6ad0-9a48-7176-948e-79921affe066"),
                                Url =
                                    "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Fplatform.localhost%3FdontChooseReportee%3Dtrue",
                                MediaType = "application/pdf",
                                ConsumerType = AttachmentUrlConsumerType.Gui
                            }
                        ]
                    },
                ],
                Transmissions = [],
                GuiActions =
                [
                    new GuiActionDto
                    {
                        Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.Delete),
                        Action = "delete",
                        Url = "http://adapter.localhost/api/v1/instance/id",
                        AuthorizationAttribute = null,
                        IsDeleteDialogAction = true,
                        HttpMethod = HttpVerb.DELETE,
                        Priority = DialogGuiActionPriority.Secondary,
                        Title =
                        [
                            new LocalizationDto { LanguageCode = "nb", Value = "Slett" },
                            new LocalizationDto { LanguageCode = "nn", Value = "Slett" },
                            new LocalizationDto { LanguageCode = "en", Value = "Delete" }
                        ],
                        Prompt = null
                    },
                    new GuiActionDto
                    {
                        Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.GoTo),
                        Action = "write",
                        Url =
                            "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Forg.apps.altinn.localhost%2Fappid%2F%3FdontChooseReportee%3Dtrue%23%2Finstance%2Fid",
                        AuthorizationAttribute = null,
                        IsDeleteDialogAction = false,
                        HttpMethod = HttpVerb.GET,
                        Priority = DialogGuiActionPriority.Primary,
                        Title =
                        [
                            new LocalizationDto { LanguageCode = "nb", Value = "Gå til skjemautfylling" },
                            new LocalizationDto { LanguageCode = "nn", Value = "Gå til skjemautfylling" },
                            new LocalizationDto { LanguageCode = "en", Value = "Go to form completion" }
                        ],
                        Prompt = null
                    }
                ],
                ApiActions = [],
                Activities =
                [
                    new ActivityDto
                    {
                        Id = Guid.Parse("00e3c7a8-2248-7fdf-a7ec-f85248d2293c"),
                        CreatedAt = new DateTime(2001, 1, 1, 1, 1, 1),
                        ExtendedType = null,
                        Type = DialogActivityType.DialogCreated,
                        TransmissionId = null,
                        PerformedBy = new ActorDto
                        {
                            ActorType = ActorType.PartyRepresentative,
                            ActorName = "Leif",
                            ActorId = null
                        },
                        Description = []
                    },
                ],
                Deleted = false
            });
        }
    }

    [Fact(DisplayName = "Given DataElements and disabled transmissions, DataElements should be mapped to Attachments")]
    public async Task Merge_WithOnlyAttachmentsAndDisabledTransmissions_MergesIntoAttachmentsAsExpected()
    {
        var mergeDto = new MergeDto(
            Application: new Application
            {
                Title = new Dictionary<string, string> { { "nb", "title" } },
                MessageBoxConfig = new MessageBoxConfig
                {
                    SyncAdapterSettings = new SyncAdapterSettings { DisableAddTransmissions = true }
                },
                DataTypes =
                [
                    new DataType
                    {
                        Id = null,
                        Description = new LanguageString { { "nb", "ikke i Gui: Uten ID" } },
                        AllowedContentTypes = null,
                        AllowedContributors = null,
                        AppLogic = null,
                        TaskId = null,
                        MaxSize = null,
                        MaxCount = 0,
                        MinCount = 0,
                        Grouping = null,
                        EnablePdfCreation = false,
                        EnableFileScan = false,
                        ValidationErrorOnPendingFileScan = false,
                        EnabledFileAnalysers = null,
                        EnabledFileValidators = null,
                        AllowedKeysForUserDefinedMetadata = null
                    },
                    new DataType
                    {
                        Id = "ref-data-as-pdf",
                        Description = new LanguageString { { "nb", "I gui: ID er ref-data-as-pdf" } },
                        AllowedContentTypes = null,
                        AllowedContributors = null,
                        AppLogic = null,
                        TaskId = null,
                        MaxSize = null,
                        MaxCount = 0,
                        MinCount = 0,
                        Grouping = null,
                        EnablePdfCreation = false,
                        EnableFileScan = false,
                        ValidationErrorOnPendingFileScan = false,
                        EnabledFileAnalysers = null,
                        EnabledFileValidators = null,
                        AllowedKeysForUserDefinedMetadata = null
                    },
                    new DataType
                    {
                        Id = "app-logic",
                        Description = new LanguageString { { "nb", "Ikke i Gui: AppLogic eksisterer" } },
                        AllowedContentTypes = null,
                        AllowedContributors = null,
                        AppLogic = new ApplicationLogic
                        {
                            AutoCreate = false,
                            ClassRef = "no.digdir.ClassRef",
                            SchemaRef = "123",
                            AllowAnonymousOnStateless = false,
                            AutoDeleteOnProcessEnd = false,
                            DisallowUserCreate = false,
                            DisallowUserDelete = false,
                            ShadowFields = null
                        },
                        TaskId = null,
                        MaxSize = null,
                        MaxCount = 0,
                        MinCount = 0,
                        Grouping = null,
                        EnablePdfCreation = false,
                        EnableFileScan = false,
                        ValidationErrorOnPendingFileScan = false,
                        EnabledFileAnalysers = null,
                        EnabledFileValidators = null,
                        AllowedKeysForUserDefinedMetadata = null
                    },
                    new DataType
                    {
                        Id = "app-owned",
                        Description = new LanguageString { { "nb", "Ikke i Gui: app:owned contributor" } },
                        AllowedContentTypes = null,
                        AllowedContributors = ["app:owned"],
                        AppLogic = null,
                        TaskId = null,
                        MaxSize = null,
                        MaxCount = 0,
                        MinCount = 0,
                        Grouping = null,
                        EnablePdfCreation = false,
                        EnableFileScan = false,
                        ValidationErrorOnPendingFileScan = false,
                        EnabledFileAnalysers = null,
                        EnabledFileValidators = null,
                        AllowedKeysForUserDefinedMetadata = null
                    },
                    new DataType
                    {
                        Id = "not-excluded",
                        Description = new LanguageString { { "nb", "I Gui: Matcher ingen regler" } },
                        AllowedContentTypes = null,
                        AllowedContributors = null,
                        AppLogic = null,
                        TaskId = null,
                        MaxSize = null,
                        MaxCount = 0,
                        MinCount = 0,
                        Grouping = null,
                        EnablePdfCreation = false,
                        EnableFileScan = false,
                        ValidationErrorOnPendingFileScan = false,
                        EnabledFileAnalysers = null,
                        EnabledFileValidators = null,
                        AllowedKeysForUserDefinedMetadata = null
                    },
                ]
            },
            ApplicationTexts: new ApplicationTexts
            {
                Translations = new Dictionary<string, ApplicationTextsTranslation>()
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents =
                [
                    new InstanceEvent
                    {
                        Id = Guid.Parse("019bc71b-eaf7-7fdf-a7ec-f85248d2293c"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 1),
                        EventType = nameof(InstanceEventType.Created),
                        User = new PlatformUser
                        {
                            UserId = UserIdWithDisplayName,
                        },
                    },
                ]
            },
            Instance: new Instance
            {
                Created = new DateTime(2000, 1, 1, 1, 1, 1),
                CreatedBy = "Me",
                LastChanged = new DateTime(2000, 1, 1, 1, 1, 2),
                LastChangedBy = "Me",
                Id = "id",
                InstanceOwner = new InstanceOwner
                {
                    PartyId = "partyId",
                },
                AppId = "appid",
                Org = "org",
                SelfLinks = null,
                DueBefore = null,
                VisibleAfter = null,
                Process = new ProcessState(),
                Status = new InstanceStatus(),
                CompleteConfirmations = [],
                Data =
                [
                    new DataElement
                    {
                        Created = new DateTime(2000, 1, 1, 1, 1, 1),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1),
                        LastChangedBy = "123456789",
                        Id = "019bd57e-ce5e-74ed-8130-3a1ac8af3d91",
                        InstanceGuid = "019bd57f-7146-73fa-9292-e6401d8ef5e8",
                        DataType = null,
                        Filename = "visible-because-of-missing-data-type",
                        ContentType = "application/pdf",
                        BlobStoragePath = "/pdfs",
                        SelfLinks = new ResourceLinks
                        {
                            Apps = null,
                            Platform = "http://platform.localhost"
                        },
                        Size = 1024,
                        ContentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                        Locked = false,
                        Refs = [Guid.Parse("019bd582-ef52-7850-a483-94ba72e9bba5")],
                        IsRead = false,
                        Tags = ["viktig", "konfidensiell"],
                        UserDefinedMetadata = [new KeyValueEntry { Key = "eier", Value = "Trond" }],
                        Metadata = [new KeyValueEntry { Key = "versjon", Value = "2" }],
                        DeleteStatus = null,
                        FileScanResult = FileScanResult.NotApplicable,
                        References =
                        [
                            new Reference
                            {
                                Value = "https://localhost",
                                Relation = RelationType.GeneratedFrom,
                                ValueType = ReferenceType.DataElement
                            }
                        ]
                    },
                    new DataElement
                    {
                        Created = new DateTime(2000, 1, 1, 1, 1, 1),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1),
                        LastChangedBy = "123456789",
                        Id = "019bd5eb-4239-7a40-a823-7735059ef136",
                        InstanceGuid = "019bd57f-7146-73fa-9292-e6401d8ef5e8",
                        DataType = "ref-data-as-pdf",
                        Filename = "visible-because-pdf-ref",
                        ContentType = "application/pdf",
                        BlobStoragePath = "/pdfs",
                        SelfLinks = new ResourceLinks
                        {
                            Apps = null,
                            Platform = "http://platform.localhost"
                        },
                        Size = 1024,
                        ContentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                        Locked = false,
                        Refs = [Guid.Parse("019bd582-ef52-7850-a483-94ba72e9bba5")],
                        IsRead = false,
                        Tags = ["viktig", "konfidensiell"],
                        UserDefinedMetadata = [new KeyValueEntry { Key = "eier", Value = "Trond" }],
                        Metadata = [new KeyValueEntry { Key = "versjon", Value = "2" }],
                        DeleteStatus = null,
                        FileScanResult = FileScanResult.NotApplicable,
                        References =
                        [
                            new Reference
                            {
                                Value = "https://localhost",
                                Relation = RelationType.GeneratedFrom,
                                ValueType = ReferenceType.DataElement
                            }
                        ]
                    },
                    new DataElement
                    {
                        Created = new DateTime(2000, 1, 1, 1, 1, 1),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1),
                        LastChangedBy = "123456789",
                        Id = "019bd5eb-62e2-711d-b79f-835d26cd1a58",
                        InstanceGuid = "019bd57f-7146-73fa-9292-e6401d8ef5e8",
                        DataType = "app-logic",
                        Filename = "not-visible-because-app-logic-exists",
                        ContentType = "application/pdf",
                        BlobStoragePath = "/pdfs",
                        SelfLinks = new ResourceLinks
                        {
                            Apps = null,
                            Platform = "http://platform.localhost"
                        },
                        Size = 1024,
                        ContentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                        Locked = false,
                        Refs = [Guid.Parse("019bd582-ef52-7850-a483-94ba72e9bba5")],
                        IsRead = false,
                        Tags = ["viktig", "konfidensiell"],
                        UserDefinedMetadata = [new KeyValueEntry { Key = "eier", Value = "Trond" }],
                        Metadata = [new KeyValueEntry { Key = "versjon", Value = "2" }],
                        DeleteStatus = null,
                        FileScanResult = FileScanResult.NotApplicable,
                        References =
                        [
                            new Reference
                            {
                                Value = "https://localhost",
                                Relation = RelationType.GeneratedFrom,
                                ValueType = ReferenceType.DataElement
                            }
                        ]
                    },
                    new DataElement
                    {
                        Created = new DateTime(2000, 1, 1, 1, 1, 1),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1),
                        LastChangedBy = "123456789",
                        Id = "019bd5eb-97ba-79a8-9f37-3ef8f82b2d0b",
                        InstanceGuid = "019bd57f-7146-73fa-9292-e6401d8ef5e8",
                        DataType = "app-owned",
                        Filename = "not-visible-because-app-owned",
                        ContentType = "application/pdf",
                        BlobStoragePath = "/pdfs",
                        SelfLinks = new ResourceLinks
                        {
                            Apps = null,
                            Platform = "http://platform.localhost"
                        },
                        Size = 1024,
                        ContentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                        Locked = false,
                        Refs = [Guid.Parse("019bd582-ef52-7850-a483-94ba72e9bba5")],
                        IsRead = false,
                        Tags = ["viktig", "konfidensiell"],
                        UserDefinedMetadata = [new KeyValueEntry { Key = "eier", Value = "Trond" }],
                        Metadata = [new KeyValueEntry { Key = "versjon", Value = "2" }],
                        DeleteStatus = null,
                        FileScanResult = FileScanResult.NotApplicable,
                        References =
                        [
                            new Reference
                            {
                                Value = "https://localhost",
                                Relation = RelationType.GeneratedFrom,
                                ValueType = ReferenceType.DataElement
                            }
                        ]
                    },
                    new DataElement
                    {
                        Created = new DateTime(2000, 1, 1, 1, 1, 1),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1),
                        LastChangedBy = "123456789",
                        Id = "019bd5eb-bd4f-7176-948e-79921affe066",
                        InstanceGuid = "019bd57f-7146-73fa-9292-e6401d8ef5e8",
                        DataType = "not-excluded",
                        Filename = "visible-not-excluded",
                        ContentType = "application/pdf",
                        BlobStoragePath = "/pdfs",
                        SelfLinks = new ResourceLinks
                        {
                            Apps = null,
                            Platform = "http://platform.localhost"
                        },
                        Size = 1024,
                        ContentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                        Locked = false,
                        Refs = [Guid.Parse("019bd582-ef52-7850-a483-94ba72e9bba5")],
                        IsRead = false,
                        Tags = ["viktig", "konfidensiell"],
                        UserDefinedMetadata = [new KeyValueEntry { Key = "eier", Value = "Trond" }],
                        Metadata = [new KeyValueEntry { Key = "versjon", Value = "2" }],
                        DeleteStatus = null,
                        FileScanResult = FileScanResult.NotApplicable,
                        References =
                        [
                            new Reference
                            {
                                Value = "https://localhost",
                                Relation = RelationType.GeneratedFrom,
                                ValueType = ReferenceType.DataElement
                            }
                        ]
                    },
                ],
                PresentationTexts = new Dictionary<string, string>(),
                DataValues = new Dictionary<string, string>()
            },
            ExistingDialog: null,
            IsMigration: false
        );
        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        using (new AssertionScope { FormattingOptions = { MaxLines = 500 } })
        {
            actualDialogDto.Should().BeEquivalentTo(new DialogDto
            {
                Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
                IsApiOnly = false,
                Revision = null,
                ServiceResource = "urn:altinn:resource:app_appid",
                Party = "urn:actor.by.party.id",
                Progress = null,
                ExtendedStatus = null,
                ExternalReference = null,
                VisibleFrom = null,
                DueAt = null,
                Process = null,
                PrecedingProcess = null,
                ExpiresAt = null,
                CreatedAt = new DateTime(2000, 1, 1, 1, 1, 1),
                UpdatedAt = new DateTime(2000, 1, 1, 1, 1, 2),
                Status = DialogStatus.Draft,
                SystemLabel = SystemLabel.Default,
                ServiceOwnerContext = new ServiceOwnerContext
                {
                    ServiceOwnerLabels =
                    [
                        new ServiceOwnerLabel { Value = "urn:altinn:integration:storage:id" }
                    ]
                },
                Content = new ContentDto
                {
                    Summary = new ContentValueDto
                    {
                        Value =
                        [
                            new LocalizationDto
                                { LanguageCode = "nb", Value = "Innsendingen er klar for å fylles ut." },
                            new LocalizationDto
                                { LanguageCode = "nn", Value = "Innsendinga er klar til å fyllast ut." },
                            new LocalizationDto
                                { LanguageCode = "en", Value = "The submission is ready to be filled out." }
                        ],
                        MediaType = "text/plain"
                    },
                    Title = new ContentValueDto
                    {
                        Value = [new LocalizationDto { Value = "title", LanguageCode = "nb" }],
                        MediaType = "text/plain",
                    },
                    ExtendedStatus = null
                },
                SearchTags = [],
                Attachments =
                [
                    new AttachmentDto
                    {
                        Id = Guid.Parse("00dc6ad0-9a48-74ed-8130-3a1ac8af3d91"),
                        DisplayName =
                        [
                            new LocalizationDto
                            {
                                Value = "visible-because-of-missing-data-type",
                                LanguageCode = "nb"
                            }
                        ],
                        Urls =
                        [
                            new AttachmentUrlDto
                            {
                                Id = Guid.Parse("00dc6ad0-9a48-74ed-8130-3a1ac8af3d91"),
                                Url =
                                    "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Fplatform.localhost%3FdontChooseReportee%3Dtrue",
                                MediaType = "application/pdf",
                                ConsumerType = AttachmentUrlConsumerType.Gui
                            }
                        ]
                    },
                    new AttachmentDto
                    {
                        Id = Guid.Parse("00dc6ad0-9a48-711d-b79f-835d26cd1a58"),
                        DisplayName =
                        [
                            new LocalizationDto
                            {
                                Value = "not-visible-because-app-logic-exists",
                                LanguageCode = "nb"
                            }
                        ],
                        Urls =
                        [
                            new AttachmentUrlDto
                            {
                                Id = Guid.Parse("00dc6ad0-9a48-711d-b79f-835d26cd1a58"),
                                Url = "http://platform.localhost",
                                MediaType = "application/pdf",
                                ConsumerType = AttachmentUrlConsumerType.Api
                            }
                        ]
                    },
                    new AttachmentDto
                    {
                        Id = Guid.Parse("00dc6ad0-9a48-79a8-9f37-3ef8f82b2d0b"),
                        DisplayName =
                        [
                            new LocalizationDto
                            {
                                Value = "not-visible-because-app-owned",
                                LanguageCode = "nb"
                            }
                        ],
                        Urls =
                        [
                            new AttachmentUrlDto
                            {
                                Id = Guid.Parse("00dc6ad0-9a48-79a8-9f37-3ef8f82b2d0b"),
                                Url = "http://platform.localhost",
                                MediaType = "application/pdf",
                                ConsumerType = AttachmentUrlConsumerType.Api
                            }
                        ]
                    },
                    new AttachmentDto
                    {
                        Id = Guid.Parse("00dc6ad0-9a48-7176-948e-79921affe066"),
                        DisplayName =
                        [
                            new LocalizationDto
                            {
                                Value = "visible-not-excluded",
                                LanguageCode = "nb"
                            }
                        ],
                        Urls =
                        [
                            new AttachmentUrlDto
                            {
                                Id = Guid.Parse("00dc6ad0-9a48-7176-948e-79921affe066"),
                                Url =
                                    "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Fplatform.localhost%3FdontChooseReportee%3Dtrue",
                                MediaType = "application/pdf",
                                ConsumerType = AttachmentUrlConsumerType.Gui
                            }
                        ]
                    },
                ],
                Transmissions = [],
                GuiActions =
                [
                    new GuiActionDto
                    {
                        Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.Delete),
                        Action = "delete",
                        Url = "http://adapter.localhost/api/v1/instance/id",
                        AuthorizationAttribute = null,
                        IsDeleteDialogAction = true,
                        HttpMethod = HttpVerb.DELETE,
                        Priority = DialogGuiActionPriority.Secondary,
                        Title =
                        [
                            new LocalizationDto { LanguageCode = "nb", Value = "Slett" },
                            new LocalizationDto { LanguageCode = "nn", Value = "Slett" },
                            new LocalizationDto { LanguageCode = "en", Value = "Delete" }
                        ],
                        Prompt = null
                    },
                    new GuiActionDto
                    {
                        Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.GoTo),
                        Action = "write",
                        Url =
                            "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Forg.apps.altinn.localhost%2Fappid%2F%3FdontChooseReportee%3Dtrue%23%2Finstance%2Fid",
                        AuthorizationAttribute = null,
                        IsDeleteDialogAction = false,
                        HttpMethod = HttpVerb.GET,
                        Priority = DialogGuiActionPriority.Primary,
                        Title =
                        [
                            new LocalizationDto { LanguageCode = "nb", Value = "Gå til skjemautfylling" },
                            new LocalizationDto { LanguageCode = "nn", Value = "Gå til skjemautfylling" },
                            new LocalizationDto { LanguageCode = "en", Value = "Go to form completion" }
                        ],
                        Prompt = null
                    }
                ],
                ApiActions = [],
                Activities =
                [
                    new ActivityDto
                    {
                        Id = Guid.Parse("00e3c7a8-2248-7fdf-a7ec-f85248d2293c"),
                        CreatedAt = new DateTime(2001, 1, 1, 1, 1, 1),
                        ExtendedType = null,
                        Type = DialogActivityType.DialogCreated,
                        TransmissionId = null,
                        PerformedBy = new ActorDto
                        {
                            ActorType = ActorType.PartyRepresentative,
                            ActorName = "Leif",
                            ActorId = null
                        },
                        Description = []
                    },
                ],
                Deleted = false
            });
        }
    }

    [Fact(DisplayName = "Given DataElements with enabled Transmissions, DataElements are split into attachments and transmissions")]
    public async Task Merge_WithAttachmentsAndTransmissions_MergesIntoAttachmentsAndTransmissionsAsExpected()
    {
        var mergeDto = new MergeDto(
            Application: new Application
            {
                Title = new Dictionary<string, string> { { "nb", "title" } },
                DataTypes =
                [
                    new DataType
                    {
                        Id = "png-0",
                        Description = new LanguageString { { "nb", "Ikke i transmission: Ikke i med activity" } },
                        AllowedContentTypes = null,
                        AllowedContributors = null,
                        AppLogic = null,
                        TaskId = null,
                        MaxSize = null,
                        MaxCount = 0,
                        MinCount = 0,
                        Grouping = null,
                        EnablePdfCreation = false,
                        EnableFileScan = false,
                        ValidationErrorOnPendingFileScan = false,
                        EnabledFileAnalysers = null,
                        EnabledFileValidators = null,
                        AllowedKeysForUserDefinedMetadata = null
                    },
                    new DataType
                    {
                        Id = "png-1",
                        Description = new LanguageString { { "nb", "I gui: ID er ref-data-as-pdf" } },
                        AllowedContentTypes = null,
                        AllowedContributors = null,
                        AppLogic = null,
                        TaskId = null,
                        MaxSize = null,
                        MaxCount = 0,
                        MinCount = 0,
                        Grouping = null,
                        EnablePdfCreation = false,
                        EnableFileScan = false,
                        ValidationErrorOnPendingFileScan = false,
                        EnabledFileAnalysers = null,
                        EnabledFileValidators = null,
                        AllowedKeysForUserDefinedMetadata = null
                    },
                    new DataType
                    {
                        Id = "png-2",
                        Description = new LanguageString { { "nb", "I gui: ID er ref-data-as-pdf" } },
                        AllowedContentTypes = null,
                        AllowedContributors = null,
                        AppLogic = null,
                        TaskId = null,
                        MaxSize = null,
                        MaxCount = 0,
                        MinCount = 0,
                        Grouping = null,
                        EnablePdfCreation = false,
                        EnableFileScan = false,
                        ValidationErrorOnPendingFileScan = false,
                        EnabledFileAnalysers = null,
                        EnabledFileValidators = null,
                        AllowedKeysForUserDefinedMetadata = null
                    },
                ]
            },
            ApplicationTexts: new ApplicationTexts
            {
                Translations = new Dictionary<string, ApplicationTextsTranslation>()
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents =
                [
                    new InstanceEvent
                    {
                        Id = Guid.Parse("019bc71b-eaf7-7fdf-a7ec-f85248d2293c"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 1),
                        EventType = nameof(InstanceEventType.Created),
                        User = new PlatformUser
                        {
                            UserId = UserIdWithDisplayName,
                        },
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("019bc71b-eaf7-7fdf-a7ec-f85248d2293c"),
                        Created = new DateTime(2001, 2, 1, 1, 1, 1),
                        EventType = nameof(InstanceEventType.Submited),
                        User = new PlatformUser
                        {
                            UserId = UserIdWithDisplayName,
                        },
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("019bc71b-eaf7-7fdf-a7ec-f85248d2293c"),
                        Created = new DateTime(2002, 2, 1, 1, 1, 1),
                        EventType = nameof(InstanceEventType.Submited),
                        User = new PlatformUser
                        {
                            UserId = UserIdWithDisplayName,
                        },
                    },
                ]
            },
            Instance: new Instance
            {
                Created = new DateTime(2000, 1, 1, 1, 1, 1),
                CreatedBy = "Me",
                LastChanged = new DateTime(2000, 2, 1, 1, 1, 1),
                LastChangedBy = "Me",
                Id = "id",
                InstanceOwner = new InstanceOwner
                {
                    PartyId = "partyId",
                },
                AppId = "appid",
                Org = "org",
                SelfLinks = null,
                DueBefore = null,
                VisibleAfter = null,
                Process = new ProcessState(),
                Status = new InstanceStatus(),
                CompleteConfirmations = [],
                Data =
                [
                    new DataElement
                    {
                        Created = new DateTime(2000, 1, 1, 1, 1, 1),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 2, 1, 1, 1, 1),
                        LastChangedBy = "123456789",
                        Id = "019bd57e-ce5e-74ed-8130-3a1ac8af3d91",
                        InstanceGuid = "019bd57f-7146-73fa-9292-e6401d8ef5e8",
                        DataType = null,
                        Filename = "outside-transmission",
                        ContentType = "application/pdf",
                        BlobStoragePath = "/pdfs",
                        SelfLinks = new ResourceLinks
                        {
                            Apps = null,
                            Platform = "http://platform.localhost"
                        },
                        Size = 1024,
                        ContentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                        Locked = false,
                        Refs = [Guid.Parse("019bd582-ef52-7850-a483-94ba72e9bba5")],
                        IsRead = false,
                        Tags = ["viktig", "konfidensiell"],
                        UserDefinedMetadata = [new KeyValueEntry { Key = "eier", Value = "Trond" }],
                        Metadata = [new KeyValueEntry { Key = "versjon", Value = "2" }],
                        DeleteStatus = null,
                        FileScanResult = FileScanResult.NotApplicable,
                        References =
                        [
                            new Reference
                            {
                                Value = "https://localhost",
                                Relation = RelationType.GeneratedFrom,
                                ValueType = ReferenceType.DataElement
                            }
                        ]
                    },
                    new DataElement
                    {
                        Created = new DateTime(2001, 1, 1, 1, 1, 1),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2001, 2, 1, 1, 1, 1),
                        LastChangedBy = "12345678911",
                        Id = "019bd5eb-4239-7a40-a823-7735059ef136",
                        InstanceGuid = "019bd57f-7146-73fa-9292-e6401d8ef5e8",
                        DataType = "png-1",
                        Filename = "in-transmission-as-1",
                        ContentType = "application/pdf",
                        BlobStoragePath = "/pdfs",
                        SelfLinks = new ResourceLinks
                        {
                            Apps = null,
                            Platform = "http://platform.localhost"
                        },
                        Size = 1024,
                        ContentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                        Locked = false,
                        Refs = [Guid.Parse("019bd582-ef52-7850-a483-94ba72e9bba5")],
                        IsRead = false,
                        Tags = ["viktig", "konfidensiell"],
                        UserDefinedMetadata = [new KeyValueEntry { Key = "eier", Value = "Trond" }],
                        Metadata = [new KeyValueEntry { Key = "versjon", Value = "2" }],
                        DeleteStatus = null,
                        FileScanResult = FileScanResult.NotApplicable,
                        References =
                        [
                            new Reference
                            {
                                Value = "https://localhost",
                                Relation = RelationType.GeneratedFrom,
                                ValueType = ReferenceType.DataElement
                            }
                        ]
                    },
                    new DataElement
                    {
                        Created = new DateTime(2002, 1, 1, 1, 1, 1),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2002, 2, 1, 1, 1, 1),
                        LastChangedBy = "12345678911",
                        Id = "019bd5eb-62e2-711d-b79f-835d26cd1a58",
                        InstanceGuid = "019bd57f-7146-73fa-9292-e6401d8ef5e8",
                        DataType = "png-2",
                        Filename = "in-transmission-as-2",
                        ContentType = "application/pdf",
                        BlobStoragePath = "/pdfs",
                        SelfLinks = new ResourceLinks
                        {
                            Apps = null,
                            Platform = "http://platform.localhost"
                        },
                        Size = 1024,
                        ContentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                        Locked = false,
                        Refs = [Guid.Parse("019bd582-ef52-7850-a483-94ba72e9bba5")],
                        IsRead = false,
                        Tags = ["viktig", "konfidensiell"],
                        UserDefinedMetadata = [new KeyValueEntry { Key = "eier", Value = "Trond" }],
                        Metadata = [new KeyValueEntry { Key = "versjon", Value = "2" }],
                        DeleteStatus = null,
                        FileScanResult = FileScanResult.NotApplicable,
                        References =
                        [
                            new Reference
                            {
                                Value = "https://localhost",
                                Relation = RelationType.GeneratedFrom,
                                ValueType = ReferenceType.DataElement
                            }
                        ]
                    },
                ],
                PresentationTexts = new Dictionary<string, string>(),
                DataValues = new Dictionary<string, string>()
            },
            ExistingDialog: null,
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        using (new AssertionScope { FormattingOptions = { MaxLines = 500 } })
        {
            actualDialogDto.Should().BeEquivalentTo(new DialogDto
            {
                Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
                IsApiOnly = false,
                Revision = null,
                ServiceResource = "urn:altinn:resource:app_appid",
                Party = "urn:actor.by.party.id",
                Progress = null,
                ExtendedStatus = null,
                ExternalReference = null,
                VisibleFrom = null,
                DueAt = null,
                Process = null,
                PrecedingProcess = null,
                ExpiresAt = null,
                CreatedAt = new DateTime(2000, 1, 1, 1, 1, 1),
                UpdatedAt = new DateTime(2000, 2, 1, 1, 1, 1),
                Status = DialogStatus.Draft,
                SystemLabel = SystemLabel.Default,
                ServiceOwnerContext = new ServiceOwnerContext
                {
                    ServiceOwnerLabels =
                    [
                        new ServiceOwnerLabel { Value = "urn:altinn:integration:storage:id" }
                    ]
                },
                Content = new ContentDto
                {
                    Summary = new ContentValueDto
                    {
                        Value =
                        [
                            new LocalizationDto
                                { LanguageCode = "nb", Value = "Innsendingen er klar for å fylles ut." },
                            new LocalizationDto
                                { LanguageCode = "nn", Value = "Innsendinga er klar til å fyllast ut." },
                            new LocalizationDto
                                { LanguageCode = "en", Value = "The submission is ready to be filled out." }
                        ],
                        MediaType = "text/plain"
                    },
                    Title = new ContentValueDto
                    {
                        Value = [new LocalizationDto { Value = "title", LanguageCode = "nb" }],
                        MediaType = "text/plain",
                    },
                    ExtendedStatus = null
                },
                SearchTags = [],
                Attachments =
                [
                    new AttachmentDto
                    {
                        Id = Guid.Parse("00dc6ad0-9a48-74ed-8130-3a1ac8af3d91"),
                        DisplayName =
                        [
                            new LocalizationDto
                            {
                                Value = "outside-transmission",
                                LanguageCode = "nb"
                            }
                        ],
                        Urls =
                        [
                            new AttachmentUrlDto
                            {
                                Id = Guid.Parse("00dc6ad0-9a48-74ed-8130-3a1ac8af3d91"),
                                Url =
                                    "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Fplatform.localhost%3FdontChooseReportee%3Dtrue",
                                MediaType = "application/pdf",
                                ConsumerType = AttachmentUrlConsumerType.Gui
                            }
                        ]
                    },
                ],
                Transmissions =
                [
                    new TransmissionDto
                    {
                        Id = Guid.Parse("00e4674d-4648-7fdf-a7ec-f85248d2293c"),
                        CreatedAt = new DateTime(2001, 2, 1, 1, 1, 1),
                        AuthorizationAttribute = null,
                        ExtendedType = null,
                        RelatedTransmissionId = null,
                        Type = DialogTransmissionType.Submission,
                        Sender = new ActorDto
                        {
                            ActorType = ActorType.PartyRepresentative,
                            ActorName = "Leif",
                            ActorId = null
                        },
                        Content = new TransmissionContentDto
                        {
                            Title = new ContentValueDto
                            {
                                Value =
                                [
                                    new LocalizationDto { Value = "Innsending #1", LanguageCode = "nb" },
                                    new LocalizationDto { Value = "Innsending #1", LanguageCode = "nn" },
                                    new LocalizationDto { Value = "Submission #1", LanguageCode = "en" }
                                ],
                                MediaType = "text/plain"
                            },
                            Summary = null,
                            ContentReference = null
                        },
                        Attachments =
                        [
                            new TransmissionAttachmentDto
                            {
                                Id = Guid.Parse("00e3c7a8-2248-7b14-9f84-39bcaa8088c1"),
                                DisplayName =
                                [
                                    new LocalizationDto
                                    {
                                        Value = "in-transmission-as-1",
                                        LanguageCode = "nb"
                                    }
                                ],
                                Urls =
                                [
                                    new TransmissionAttachmentUrlDto
                                    {
                                        Url =
                                            "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Fplatform.localhost%3FdontChooseReportee%3Dtrue",
                                        MediaType = "application/pdf",
                                        ConsumerType = AttachmentUrlConsumerType.Gui
                                    }
                                ]
                            },
                            new TransmissionAttachmentDto
                            {
                                Id = Guid.Parse("00e4674d-4648-7b33-acbb-81d5b97663bd"),
                                DisplayName =
                                [
                                    new LocalizationDto
                                    {
                                        Value = "Kvittering",
                                        LanguageCode = "nb"
                                    },
                                    new LocalizationDto
                                    {
                                        Value = "Kvittering",
                                        LanguageCode = "nn"
                                    },
                                    new LocalizationDto
                                    {
                                        Value = "Receipt",
                                        LanguageCode = "en"
                                    },
                                ],
                                Urls =
                                [
                                    new TransmissionAttachmentUrlDto
                                    {
                                        Url =
                                            "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Fplatform.altinn.localhost%2Freceipt%2Fid%3FdontChooseReportee%3Dtrue",
                                        MediaType = "text/html",
                                        ConsumerType = AttachmentUrlConsumerType.Gui
                                    }
                                ]
                            },
                        ]
                    },
                    new TransmissionDto
                    {
                        Id = Guid.Parse("00ebbefe-7248-7fdf-a7ec-f85248d2293c"),
                        CreatedAt = new DateTime(2002, 2, 1, 1, 1, 1),
                        AuthorizationAttribute = null,
                        ExtendedType = null,
                        RelatedTransmissionId = null,
                        Type = DialogTransmissionType.Submission,
                        Sender = new ActorDto
                        {
                            ActorType = ActorType.PartyRepresentative,
                            ActorName = "Leif",
                            ActorId = null
                        },
                        Content = new TransmissionContentDto
                        {
                            Title = new ContentValueDto
                            {
                                Value =
                                [
                                    new LocalizationDto { Value = "Innsending #2", LanguageCode = "nb" },
                                    new LocalizationDto { Value = "Innsending #2", LanguageCode = "nn" },
                                    new LocalizationDto { Value = "Submission #2", LanguageCode = "en" }
                                ],
                                MediaType = "text/plain"
                            },
                            Summary = null,
                            ContentReference = null
                        },
                        Attachments =
                        [
                            new TransmissionAttachmentDto
                            {
                                Id = Guid.Parse("00eb1f59-4e48-70f0-94df-db2257f255ab"),
                                DisplayName =
                                [
                                    new LocalizationDto
                                    {
                                        Value = "in-transmission-as-2",
                                        LanguageCode = "nb"
                                    }
                                ],
                                Urls =
                                [
                                    new TransmissionAttachmentUrlDto
                                    {
                                        Url =
                                            "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Fplatform.localhost%3FdontChooseReportee%3Dtrue",
                                        MediaType = "application/pdf",
                                        ConsumerType = AttachmentUrlConsumerType.Gui
                                    }
                                ]
                            },
                            new TransmissionAttachmentDto
                            {
                                Id = Guid.Parse("00ebbefe-7248-71c3-a716-f4b04aeb701d"),
                                DisplayName =
                                [
                                    new LocalizationDto
                                    {
                                        Value = "Kvittering",
                                        LanguageCode = "nb"
                                    },
                                    new LocalizationDto
                                    {
                                        Value = "Kvittering",
                                        LanguageCode = "nn"
                                    },
                                    new LocalizationDto
                                    {
                                        Value = "Receipt",
                                        LanguageCode = "en"
                                    },
                                ],
                                Urls =
                                [
                                    new TransmissionAttachmentUrlDto
                                    {
                                        Url =
                                            "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Fplatform.altinn.localhost%2Freceipt%2Fid%3FdontChooseReportee%3Dtrue",
                                        MediaType = "text/html",
                                        ConsumerType = AttachmentUrlConsumerType.Gui
                                    }
                                ]
                            }
                        ]
                    }
                ],
                GuiActions =
                [
                    new GuiActionDto
                    {
                        Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.Delete),
                        Action = "delete",
                        Url = "http://adapter.localhost/api/v1/instance/id",
                        AuthorizationAttribute = null,
                        IsDeleteDialogAction = true,
                        HttpMethod = HttpVerb.DELETE,
                        Priority = DialogGuiActionPriority.Secondary,
                        Title =
                        [
                            new LocalizationDto { LanguageCode = "nb", Value = "Slett" },
                            new LocalizationDto { LanguageCode = "nn", Value = "Slett" },
                            new LocalizationDto { LanguageCode = "en", Value = "Delete" }
                        ],
                        Prompt = null
                    },
                    new GuiActionDto
                    {
                        Id = mergeDto.DialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.GoTo),
                        Action = "write",
                        Url =
                            "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Forg.apps.altinn.localhost%2Fappid%2F%3FdontChooseReportee%3Dtrue%23%2Finstance%2Fid",
                        AuthorizationAttribute = null,
                        IsDeleteDialogAction = false,
                        HttpMethod = HttpVerb.GET,
                        Priority = DialogGuiActionPriority.Primary,
                        Title =
                        [
                            new LocalizationDto { LanguageCode = "nb", Value = "Gå til skjemautfylling" },
                            new LocalizationDto { LanguageCode = "nn", Value = "Gå til skjemautfylling" },
                            new LocalizationDto { LanguageCode = "en", Value = "Go to form completion" }
                        ],
                        Prompt = null
                    }
                ],
                ApiActions = [],
                Activities =
                [
                    new ActivityDto
                    {
                        Id = Guid.Parse("00e3c7a8-2248-7fdf-a7ec-f85248d2293c"),
                        CreatedAt = new DateTime(2001, 1, 1, 1, 1, 1),
                        ExtendedType = null,
                        Type = DialogActivityType.DialogCreated,
                        TransmissionId = null,
                        PerformedBy = new ActorDto
                        {
                            ActorType = ActorType.PartyRepresentative,
                            ActorName = "Leif",
                            ActorId = null
                        },
                        Description = []
                    },
                    new ActivityDto
                    {
                        Id = Guid.Parse("00e4674d-4648-7fdf-a7ec-f85248d2293c"),
                        CreatedAt = new DateTime(2001, 2, 1, 1, 1, 1),
                        ExtendedType = null,
                        Type = DialogActivityType.FormSubmitted,
                        TransmissionId = null,
                        PerformedBy = new ActorDto
                        {
                            ActorType = ActorType.PartyRepresentative,
                            ActorName = "Leif",
                            ActorId = null
                        },
                        Description = []
                    },
                    new ActivityDto
                    {
                        Id = Guid.Parse("00ebbefe-7248-7fdf-a7ec-f85248d2293c"),
                        CreatedAt = new DateTime(2002, 2, 1, 1, 1, 1),
                        ExtendedType = null,
                        Type = DialogActivityType.FormSubmitted,
                        TransmissionId = null,
                        PerformedBy = new ActorDto
                        {
                            ActorType = ActorType.PartyRepresentative,
                            ActorName = "Leif",
                            ActorId = null
                        },
                        Description = []
                    },
                ],
                Deleted = false
            });
        }
    }
}
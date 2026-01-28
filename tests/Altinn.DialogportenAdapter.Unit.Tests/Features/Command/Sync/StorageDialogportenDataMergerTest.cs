using Altinn.ApiClients.Maskinporten.Config;
using Altinn.DialogportenAdapter.Unit.Tests.Common.Assert;
using Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;
using Altinn.DialogportenAdapter.WebApi;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Enums;
using Altinn.Platform.Storage.Interface.Models;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Altinn.DialogportenAdapter.Unit.Tests.Features.Command.Sync;

public class StorageDialogportenDataMergerTest
{
    private readonly IRegisterRepository _registerRepositoryMock = Substitute.For<IRegisterRepository>();
    private readonly StorageDialogportenDataMerger _storageDialogportenDataMerger;
    private const string PartyId1 = "party-1";
    private const string PartyId2 = "party-2";
    private const int UserId1 = 1;
    private const int UserId2 = 2;
    private const int UserUnknown = 999;
    private AdapterFeatureFlagSettings _featureFlags = new() { EnableSubmissionTransmissions = true };

    public StorageDialogportenDataMergerTest()
    {
        AssertionOptions.FormattingOptions.MaxLines = 500;
        AssertionOptions.FormattingOptions.MaxDepth = 10;

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
            .Returns(new Dictionary<string, string>
            {
                { PartyId1, "urn:actor.by.party.id.party1" },
                { PartyId2, "urn:actor.by.party.id.party2" }
            });

        _registerRepositoryMock.GetActorUrnByUserId(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>
            {
                { $"{UserId1}", "urn:altinn:displayName:Leif" },
                { $"{UserId2}", "urn:altinn:person:legacy-selfidentified:Per" },
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
                Title = new Dictionary<string, string>
                {
                    ["nb"] = "Test applikasjon",
                    ["en"] = "Test application"
                }
            },
            ApplicationTexts: new ApplicationTexts
            {
                Translations = []
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
            Instance: AltinnInstanceBuilder.NewInProgressInstance().Build(),
            ExistingDialog: null,
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_urn:altinn:instance-id",
            Party = "urn:actor.by.party.id.party1",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = null,
            DueAt = null,
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            UpdatedAt = new DateTime(1000, 2, 1, 1, 1, 1, DateTimeKind.Utc),
            Status = DialogStatus.InProgress,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = Regulars.ServiceOwnerContexts.DefaultContext,
            Content = Regulars.Content.ReadyForSubmission,
            SearchTags = [],
            Attachments = [],
            Transmissions = [],
            GuiActions =
            [
                Regulars.GuiActions.Delete(mergeDto.DialogId),
                Regulars.GuiActions.Write(mergeDto.DialogId),
            ],
            ApiActions = [],
            Activities = [],
            Deleted = false
        });
    }

    [Fact(DisplayName = "Given two saves of a full MergeDto, should return the same DialogDto")]
    public async Task Merge_FullMergeDtoMergedTwice_ReturnsTheSameDialogDto()
    {
        var mergeDto = new MergeDto(
            Application: AltinnApplicationBuilder
                .NewDefaultAltinnApplication()
                .WithDataTypes([
                    new DataType
                    {
                        Id = "png-1",
                        Description = new LanguageString
                        {
                            {
                                "nb", "I gui: ID er ref-data-as-pdf"
                            }
                        },
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
                ])
                .WithDataFields([
                    new DataField
                    {
                        Id = "data-field-id",
                        Path = "/path",
                        DataTypeId = "data-field-data-type-id"
                    }
                ])
                .Build(),
            ApplicationTexts: new ApplicationTexts
            {
                Translations = []
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents =
                [
                    AltinnInstanceEventBuilder.NewCreatedByPlatformUserInstanceEvent(UserId1).Build(),
                    AltinnInstanceEventBuilder.NewSubmittedByPlatformUserInstanceEvent(UserId1).Build(),
                ]
            },
            Instance: AltinnInstanceBuilder
                .NewInProgressInstance()
                .WithData([
                    new DataElement
                    {
                        Created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 2, 1, 1, 1, 1, DateTimeKind.Utc),
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
                        Created = new DateTime(2001, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2001, 2, 1, 1, 1, 1, DateTimeKind.Utc),
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
                    }
                ])
                .Build(),
            ExistingDialog: null,
            IsMigration: false
        );

        var actualDialogDto1 = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);
        var actualDialogDto2 = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto1.Activities.Should().NotBeEmpty();
        actualDialogDto1.Attachments.Should().NotBeEmpty();
        actualDialogDto1.GuiActions.Should().NotBeEmpty();
        actualDialogDto1.Transmissions.Should().NotBeEmpty();
        actualDialogDto1.Content.Should().NotBeNull();
        actualDialogDto1.Should().BeEquivalentTo(actualDialogDto2);
    }


    [Fact(DisplayName =
        "Given a localized Substatus, Substatus should be truncated and mapped to ExtendedStatus in all languages")]
    public async Task Merge_LocalizedSubstatus_MapsAllLanguagesToExtendedStatus()
    {
        var mergeDto = new MergeDto(
            Application: AltinnApplicationBuilder.NewDefaultAltinnApplication().Build(),
            ApplicationTexts: new ApplicationTexts
            {
                Translations =
                [
                    new ApplicationTextsTranslation
                    {
                        Language = "nb",
                        Texts = new Dictionary<string, string>
                        {
                            { "substatus.label", "Registrering av tiltak, og litt for lang tekst" },
                            {
                                "substatus.description",
                                "øke sikkerheten og beredskapen i den digitale grunnmuren i sårbare kommuner og regioner gjennom målrettede tilskudd"
                            },
                        },
                    },
                    new ApplicationTextsTranslation
                    {
                        Language = "nn",
                        Texts = new Dictionary<string, string>
                        {
                            { "substatus.label", "Registrering av tiltak nn" },
                            {
                                "substatus.description",
                                "Auke tryggleiken og beredskapen i den digitale grunnmuren i sårbare kommunar og regionar gjennom målretta tilskot"
                            },
                        },
                    },
                    new ApplicationTextsTranslation
                    {
                        Language = "en",
                        Texts = new Dictionary<string, string>
                        {
                            { "substatus.label", "Registration of measures" },
                            {
                                "substatus.description",
                                "Increase security and preparedness in the digital infrastructure in vulnerable municipalities and regions through targeted grants"
                            },
                        }
                    },
                ]
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents = [AltinnInstanceEventBuilder.NewCreatedByPlatformUserInstanceEvent(UserId1).Build()]
            },
            Instance: AltinnInstanceBuilder.NewInProgressInstance().WithStatus(new InstanceStatus
            {
                Substatus = new Substatus
                {
                    Label = "substatus.label",
                }
            }).Build(),
            ExistingDialog: null,
            IsMigration: false);
        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);
        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_urn:altinn:instance-id",
            Party = "urn:actor.by.party.id.party1",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = null,
            DueAt = null,
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            UpdatedAt = new DateTime(1000, 2, 1, 1, 1, 1, DateTimeKind.Utc),
            Status = DialogStatus.InProgress,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = Regulars.ServiceOwnerContexts.DefaultContext,
            Content = new ContentDto
            {
                Summary = Regulars.Content.ReadyForSubmission.Summary,
                Title = Regulars.Content.ReadyForSubmission.Title,
                ExtendedStatus = new ContentValueDto
                {
                    Value =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Registrering av tiltak..." },
                        new LocalizationDto { LanguageCode = "nn", Value = "Registrering av tiltak nn" },
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
                Regulars.GuiActions.Delete(mergeDto.DialogId),
                Regulars.GuiActions.Write(mergeDto.DialogId)
            ],
            ApiActions = [],
            Activities = [],
            Deleted = false
        });
    }

    [Fact(DisplayName =
        "Given a non-localized Substatus, Substatus should be truncated and mapped to Extendedstatus in NB")]
    public async Task Merge_SubstatusWithText_AssumesNorwegian()
    {
        var mergeDto = new MergeDto(
            Application: AltinnApplicationBuilder.NewDefaultAltinnApplication().Build(),
            ApplicationTexts: new ApplicationTexts { Translations = [] },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents = [AltinnInstanceEventBuilder.NewCreatedByPlatformUserInstanceEvent(UserId1).Build()]
            },
            Instance: AltinnInstanceBuilder
                .NewInProgressInstance()
                .WithStatus(new InstanceStatus
                {
                    Substatus = new Substatus
                    {
                        Label = "En substatus som vi antar er på norsk og litt for lang",
                    }
                })
                .Build(),
            ExistingDialog: null,
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_urn:altinn:instance-id",
            Party = "urn:actor.by.party.id.party1",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = null,
            DueAt = null,
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            UpdatedAt = new DateTime(1000, 2, 1, 1, 1, 1, DateTimeKind.Utc),
            Status = DialogStatus.InProgress,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = Regulars.ServiceOwnerContexts.DefaultContext,
            Content = new ContentDto
            {
                Summary = Regulars.Content.ReadyForSubmission.Summary,
                Title = Regulars.Content.ReadyForSubmission.Title,
                ExtendedStatus = new ContentValueDto
                {
                    Value =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "En substatus som vi an..." },
                    ],
                    MediaType = "text/plain"
                },
            },
            SearchTags = [],
            Attachments = [],
            Transmissions = [],
            GuiActions =
            [
                Regulars.GuiActions.Delete(mergeDto.DialogId),
                Regulars.GuiActions.Write(mergeDto.DialogId)
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
            Application: AltinnApplicationBuilder.NewDefaultAltinnApplication().Build(),
            ApplicationTexts: new ApplicationTexts
            {
                Translations = []
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents = [AltinnInstanceEventBuilder.NewCreatedByPlatformUserInstanceEvent(UserId1).Build()]
            },
            Instance: AltinnInstanceBuilder
                .NewInProgressInstance()
                .WithDueBefore(new DateTime(900, 1, 1, 1, 1, 3))
                .WithVisibleAfter(new DateTime(900, 1, 1, 1, 1, 4)).Build()
            ,
            ExistingDialog: null,
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_urn:altinn:instance-id",
            Party = "urn:actor.by.party.id.party1",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = null,
            DueAt = null,
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            UpdatedAt = new DateTime(1000, 2, 1, 1, 1, 1, DateTimeKind.Utc),
            Status = DialogStatus.InProgress,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = Regulars.ServiceOwnerContexts.DefaultContext,
            Content = Regulars.Content.ReadyForSubmission,
            SearchTags = [],
            Attachments = [],
            Transmissions = [],
            GuiActions =
            [
                Regulars.GuiActions.Delete(mergeDto.DialogId),
                Regulars.GuiActions.Write(mergeDto.DialogId)
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
            Application: AltinnApplicationBuilder.NewDefaultAltinnApplication().Build(),
            ApplicationTexts: new ApplicationTexts
            {
                Translations = []
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents = [AltinnInstanceEventBuilder.NewCreatedByPlatformUserInstanceEvent(UserId1).Build()]
            },
            Instance: AltinnInstanceBuilder
                .NewInProgressInstance()
                .WithDueBefore(new DateTime(9999, 1, 1, 1, 1, 3))
                .WithVisibleAfter(new DateTime(9999, 1, 1, 1, 1, 4)).Build()
            ,
            ExistingDialog: null,
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_urn:altinn:instance-id",
            Party = "urn:actor.by.party.id.party1",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = new DateTime(9999, 1, 1, 1, 1, 4),
            DueAt = new DateTime(9999, 1, 1, 1, 1, 3),
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            UpdatedAt = new DateTime(1000, 2, 1, 1, 1, 1, DateTimeKind.Utc),
            Status = DialogStatus.InProgress,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = Regulars.ServiceOwnerContexts.DefaultContext,
            Content = Regulars.Content.ReadyForSubmission,
            SearchTags = [],
            Attachments = [],
            Transmissions = [],
            GuiActions =
            [
                Regulars.GuiActions.Delete(mergeDto.DialogId),
                Regulars.GuiActions.Write(mergeDto.DialogId)
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
            Application: AltinnApplicationBuilder
                .NewDefaultAltinnApplication()
                .WithMessageBoxConfig(new MessageBoxConfig
                {
                    SyncAdapterSettings = new SyncAdapterSettings { DisableAddTransmissions = true }
                })
                .Build(),
            ApplicationTexts: new ApplicationTexts
            {
                Translations = []
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents =
                [
                    new InstanceEvent
                    {
                        Id = Guid.Parse("019bc71b-eaf7-7fdf-a7ec-f85248d2293c"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        EventType = nameof(InstanceEventType.Created),
                        User = new PlatformUser
                        {
                            UserId = UserId1,
                        },
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("4ef9d179-2d4b-403f-9fcc-6f7cd619f12e"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 2, DateTimeKind.Utc),
                        EventType = nameof(InstanceEventType.Deleted),
                        User = new PlatformUser
                        {
                            UserId = UserId2,
                        }
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("b26a5762-a719-48fc-aa11-6b89c409264b"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 3, DateTimeKind.Utc),
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
                        Created = new DateTime(2001, 1, 1, 1, 1, 4, DateTimeKind.Utc),
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
                        Created = new DateTime(2001, 1, 1, 1, 1, 5, DateTimeKind.Utc),
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
                        Created = new DateTime(2001, 1, 1, 1, 1, 6, DateTimeKind.Utc),
                        EventType = nameof(InstanceEventType.SentToPayment),
                        User = new PlatformUser
                        {
                            UserId = 1,
                        }
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("5e9e8c0f-c417-4d0a-aa34-c19bb45c358b"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 7, DateTimeKind.Utc),
                        EventType = nameof(InstanceEventType.SentToFormFill),
                        User = new PlatformUser
                        {
                            UserId = 1,
                        }
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("001a6e3c-5046-4e41-a83d-b5a3ed22afb1"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 8, DateTimeKind.Utc),
                        EventType = nameof(InstanceEventType.SentToSendIn),
                        User = new PlatformUser
                        {
                            UserId = 1,
                        }
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("6c5532fd-42b8-4fe6-aebe-62ef9e035791"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 9, DateTimeKind.Utc),
                        EventType = nameof(InstanceEventType.Submited),
                        User = new PlatformUser
                        {
                            UserId = 1,
                        }
                    },
                ]
            },
            Instance: AltinnInstanceBuilder.NewInProgressInstance().Build(),
            ExistingDialog: null,
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_urn:altinn:instance-id",
            Party = "urn:actor.by.party.id.party1",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = null,
            DueAt = null,
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            UpdatedAt = new DateTime(1000, 2, 1, 1, 1, 1, DateTimeKind.Utc),
            Status = DialogStatus.Draft,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = Regulars.ServiceOwnerContexts.DefaultContext,
            Content = Regulars.Content.ReadyForSubmission,
            SearchTags = [],
            Attachments = [],
            GuiActions =
            [
                Regulars.GuiActions.Delete(mergeDto.DialogId),
                Regulars.GuiActions.Write(mergeDto.DialogId)
            ],
            ApiActions = [],
            Activities =
            [
                new ActivityDto
                {
                    Id = Guid.Parse("00e3c7df-10c8-7fdf-a7ec-f85248d2293c"),
                    CreatedAt = new DateTime(2001, 1, 1, 1, 1, 1, DateTimeKind.Utc),
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
                    Id = Guid.Parse("00e3c7df-14b0-703f-9fcc-6f7cd619f12e"),
                    CreatedAt = new DateTime(2001, 1, 1, 1, 1, 2, DateTimeKind.Utc),
                    ExtendedType = null,
                    Type = DialogActivityType.DialogDeleted,
                    TransmissionId = null,
                    PerformedBy = new ActorDto
                    {
                        ActorType = ActorType.PartyRepresentative,
                        ActorName = null,
                        ActorId = "urn:altinn:person:legacy-selfidentified:Per"
                    },
                    Description = []
                },
                new ActivityDto
                {
                    Id = Guid.Parse("00e3c7df-1898-78fc-aa11-6b89c409264b"),
                    CreatedAt = new DateTime(2001, 1, 1, 1, 1, 3, DateTimeKind.Utc),
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
                    Id = Guid.Parse("00e3c7df-1c80-7f92-a462-0522af8f17d5"),
                    CreatedAt = new DateTime(2001, 1, 1, 1, 1, 4, DateTimeKind.Utc),
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
                    Id = Guid.Parse("00e3c7df-2068-7875-8547-e747144f5952"),
                    CreatedAt = new DateTime(2001, 1, 1, 1, 1, 5, DateTimeKind.Utc),
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
                    Id = Guid.Parse("00e3c7df-2450-77fd-bc9b-c5efc2e93d61"),
                    CreatedAt = new DateTime(2001, 1, 1, 1, 1, 6, DateTimeKind.Utc),
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
                    Id = Guid.Parse("00e3c7df-2838-7d0a-aa34-c19bb45c358b"),
                    CreatedAt = new DateTime(2001, 1, 1, 1, 1, 7, DateTimeKind.Utc),
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
                    Id = Guid.Parse("00e3c7df-2c20-7e41-a83d-b5a3ed22afb1"),
                    CreatedAt = new DateTime(2001, 1, 1, 1, 1, 8, DateTimeKind.Utc),
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
                    Id = Guid.Parse("00e3c7df-3008-7fe6-aebe-62ef9e035791"),
                    CreatedAt = new DateTime(2001, 1, 1, 1, 1, 9, DateTimeKind.Utc),
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

    [Fact(DisplayName =
        "Given DataElements (only attachments) with enabled Transmissions, DataElements are mapped into Attachments")]
    public async Task Merge_WithOnlyAttachmentsAndEnabledTransmissions_MergesIntoAttachmentsAsExpected()
    {
        var mergeDto = new MergeDto(
            Application: AltinnApplicationBuilder
                .NewDefaultAltinnApplication()
                .WithDataTypes([
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
                )
                .Build(),
            ApplicationTexts: new ApplicationTexts
            {
                Translations = []
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents = [AltinnInstanceEventBuilder.NewCreatedByPlatformUserInstanceEvent(UserId1).Build()]
            },
            Instance: AltinnInstanceBuilder
                .NewInProgressInstance()
                .WithData([
                    new DataElement
                    {
                        Created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
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
                        Created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
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
                        Created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
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
                        Created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
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
                        Created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
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
                ])
                .Build()
            ,
            ExistingDialog: null,
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_urn:altinn:instance-id",
            Party = "urn:actor.by.party.id.party1",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = null,
            DueAt = null,
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            UpdatedAt = new DateTime(1000, 2, 1, 1, 1, 1, DateTimeKind.Utc),
            Status = DialogStatus.InProgress,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = Regulars.ServiceOwnerContexts.DefaultContext,
            Content = Regulars.Content.ReadyForSubmission,
            SearchTags = [],
            Attachments =
            [
                new AttachmentDto
                {
                    Id = Guid.Parse("00dc6b07-88c8-74ed-8130-3a1ac8af3d91"),
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
                            Id = Guid.Parse("00dc6b07-88c8-74ed-8130-3a1ac8af3d91"),
                            Url =
                                "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Fplatform.localhost%3FdontChooseReportee%3Dtrue",
                            MediaType = "application/pdf",
                            ConsumerType = AttachmentUrlConsumerType.Gui
                        }
                    ]
                },
                new AttachmentDto
                {
                    Id = Guid.Parse("00dc6b07-88c8-7a40-a823-7735059ef136"),
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
                            Id = Guid.Parse("00dc6b07-88c8-7a40-a823-7735059ef136"),
                            Url =
                                "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Fplatform.localhost%3FdontChooseReportee%3Dtrue",
                            MediaType = "application/pdf",
                            ConsumerType = AttachmentUrlConsumerType.Gui
                        }
                    ]
                },
                new AttachmentDto
                {
                    Id = Guid.Parse("00dc6b07-88c8-711d-b79f-835d26cd1a58"),
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
                            Id = Guid.Parse("00dc6b07-88c8-711d-b79f-835d26cd1a58"),
                            Url = "http://platform.localhost",
                            MediaType = "application/pdf",
                            ConsumerType = AttachmentUrlConsumerType.Api
                        }
                    ]
                },
                new AttachmentDto
                {
                    Id = Guid.Parse("00dc6b07-88c8-79a8-9f37-3ef8f82b2d0b"),
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
                            Id = Guid.Parse("00dc6b07-88c8-79a8-9f37-3ef8f82b2d0b"),
                            Url = "http://platform.localhost",
                            MediaType = "application/pdf",
                            ConsumerType = AttachmentUrlConsumerType.Api
                        }
                    ]
                },
                new AttachmentDto
                {
                    Id = Guid.Parse("00dc6b07-88c8-7176-948e-79921affe066"),
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
                            Id = Guid.Parse("00dc6b07-88c8-7176-948e-79921affe066"),
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
                Regulars.GuiActions.Delete(mergeDto.DialogId),
                Regulars.GuiActions.Write(mergeDto.DialogId)
            ],
            ApiActions = [],
            Activities = [],
            Deleted = false
        });
    }

    [Fact(DisplayName = "Given DataElements and disabled transmissions, DataElements should be mapped to Attachments")]
    public async Task Merge_WithOnlyAttachmentsAndDisabledTransmissions_MergesIntoAttachmentsAsExpected()
    {
        var mergeDto = new MergeDto(
            Application: AltinnApplicationBuilder
                .NewDefaultAltinnApplication()
                .WithMessageBoxConfig(new MessageBoxConfig
                {
                    SyncAdapterSettings = new SyncAdapterSettings { DisableAddTransmissions = true }
                })
                .WithDataTypes([
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
                )
                .Build(),
            ApplicationTexts: new ApplicationTexts
            {
                Translations = []
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents = [AltinnInstanceEventBuilder.NewCreatedByPlatformUserInstanceEvent(UserId1).Build()]
            },
            Instance: AltinnInstanceBuilder
                .NewInProgressInstance()
                .WithData([
                    new DataElement
                    {
                        Created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
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
                        Created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
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
                        Created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
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
                        Created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
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
                        Created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
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
                ])
                .Build()
            ,
            ExistingDialog: null,
            IsMigration: false
        );
        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_urn:altinn:instance-id",
            Party = "urn:actor.by.party.id.party1",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = null,
            DueAt = null,
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            UpdatedAt = new DateTime(1000, 2, 1, 1, 1, 1, DateTimeKind.Utc),
            Status = DialogStatus.InProgress,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = Regulars.ServiceOwnerContexts.DefaultContext,
            Content = Regulars.Content.ReadyForSubmission,
            SearchTags = [],
            Attachments =
            [
                new AttachmentDto
                {
                    Id = Guid.Parse("00dc6b07-88c8-74ed-8130-3a1ac8af3d91"),
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
                            Id = Guid.Parse("00dc6b07-88c8-74ed-8130-3a1ac8af3d91"),
                            Url =
                                "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Fplatform.localhost%3FdontChooseReportee%3Dtrue",
                            MediaType = "application/pdf",
                            ConsumerType = AttachmentUrlConsumerType.Gui
                        }
                    ]
                },
                new AttachmentDto
                {
                    Id = Guid.Parse("00dc6b07-88c8-711d-b79f-835d26cd1a58"),
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
                            Id = Guid.Parse("00dc6b07-88c8-711d-b79f-835d26cd1a58"),
                            Url = "http://platform.localhost",
                            MediaType = "application/pdf",
                            ConsumerType = AttachmentUrlConsumerType.Api
                        }
                    ]
                },
                new AttachmentDto
                {
                    Id = Guid.Parse("00dc6b07-88c8-79a8-9f37-3ef8f82b2d0b"),
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
                            Id = Guid.Parse("00dc6b07-88c8-79a8-9f37-3ef8f82b2d0b"),
                            Url = "http://platform.localhost",
                            MediaType = "application/pdf",
                            ConsumerType = AttachmentUrlConsumerType.Api
                        }
                    ]
                },
                new AttachmentDto
                {
                    Id = Guid.Parse("00dc6b07-88c8-7176-948e-79921affe066"),
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
                            Id = Guid.Parse("00dc6b07-88c8-7176-948e-79921affe066"),
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
                Regulars.GuiActions.Delete(mergeDto.DialogId),
                Regulars.GuiActions.Write(mergeDto.DialogId)
            ],
            ApiActions = [],
            Activities = [],
            Deleted = false
        });
    }

    [Fact(DisplayName =
        "Given DataElements with enabled Transmissions, DataElements are split into attachments and transmissions")]
    public async Task Merge_WithAttachmentsAndTransmissions_MergesIntoAttachmentsAndTransmissionsAsExpected()
    {
        var mergeDto = new MergeDto(
            Application: AltinnApplicationBuilder
                .NewDefaultAltinnApplication()
                .WithDataTypes([
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
                )
                .Build(),
            ApplicationTexts: new ApplicationTexts
            {
                Translations = []
            },
            DialogId: Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            Events: new InstanceEventList
            {
                InstanceEvents =
                [
                    new InstanceEvent
                    {
                        Id = Guid.Parse("019bc71b-eaf7-7fdf-a7ec-f85248d2293c"),
                        Created = new DateTime(2001, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        EventType = nameof(InstanceEventType.Created),
                        User = new PlatformUser
                        {
                            UserId = UserId1,
                        },
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("019bc71b-eaf7-7fdf-a7ec-f85248d2293c"),
                        Created = new DateTime(2001, 2, 1, 1, 1, 1, DateTimeKind.Utc),
                        EventType = nameof(InstanceEventType.Submited),
                        User = new PlatformUser
                        {
                            UserId = UserId1,
                        },
                    },
                    new InstanceEvent
                    {
                        Id = Guid.Parse("019bc71b-eaf7-7fdf-a7ec-f85248d2293c"),
                        Created = new DateTime(2002, 2, 1, 1, 1, 1, DateTimeKind.Utc),
                        EventType = nameof(InstanceEventType.Submited),
                        User = new PlatformUser
                        {
                            UserId = UserId1,
                        },
                    },
                ]
            },
            Instance: AltinnInstanceBuilder
                .NewInProgressInstance()
                .WithData([
                    new DataElement
                    {
                        Created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2000, 2, 1, 1, 1, 1, DateTimeKind.Utc),
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
                        Created = new DateTime(2001, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2001, 2, 1, 1, 1, 1, DateTimeKind.Utc),
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
                        Created = new DateTime(2002, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        CreatedBy = "me",
                        LastChanged = new DateTime(2002, 2, 1, 1, 1, 1, DateTimeKind.Utc),
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
                ])
                .Build()
            ,
            ExistingDialog: null,
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f"),
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_urn:altinn:instance-id",
            Party = "urn:actor.by.party.id.party1",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = null,
            DueAt = null,
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            UpdatedAt = new DateTime(1000, 2, 1, 1, 1, 1, DateTimeKind.Utc),
            Status = DialogStatus.Draft,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = Regulars.ServiceOwnerContexts.DefaultContext,
            Content = Regulars.Content.ReadyForSubmission,
            SearchTags = [],
            Attachments =
            [
                new AttachmentDto
                {
                    Id = Guid.Parse("00dc6b07-88c8-74ed-8130-3a1ac8af3d91"),
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
                            Id = Guid.Parse("00dc6b07-88c8-74ed-8130-3a1ac8af3d91"),
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
                    Id = Guid.Parse("00e46784-34c8-7fdf-a7ec-f85248d2293c"),
                    CreatedAt = new DateTime(2001, 2, 1, 1, 1, 1, DateTimeKind.Utc),
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
                        Summary = null!,
                        ContentReference = null
                    },
                    Attachments =
                    [
                        new TransmissionAttachmentDto
                        {
                            Id = Guid.Parse("00e3c7df-10c8-7ed4-8a0e-eec48f079b01"),
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
                        Regulars.Transmission.Attachment.Receipt(Guid.Parse("00e46784-34c8-70a1-9c93-8d4b2160d3a0"))
                    ]
                },
                new TransmissionDto
                {
                    Id = Guid.Parse("00ebbf35-60c8-7fdf-a7ec-f85248d2293c"),
                    CreatedAt = new DateTime(2002, 2, 1, 1, 1, 1, DateTimeKind.Utc),
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
                        Summary = null!,
                        ContentReference = null
                    },
                    Attachments =
                    [
                        new TransmissionAttachmentDto
                        {
                            Id = Guid.Parse("00eb1f90-3cc8-796f-bb0b-b7ad01e5e0d5"),
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
                        Regulars.Transmission.Attachment.Receipt(Guid.Parse("00ebbf35-60c8-7938-809d-550d82c654fc"))
                    ]
                }
            ],
            GuiActions =
            [
                Regulars.GuiActions.Delete(mergeDto.DialogId),
                Regulars.GuiActions.Write(mergeDto.DialogId),
            ],
            ApiActions = [],
            Activities =
            [
                new ActivityDto
                {
                    Id = Guid.Parse("00e3c7df-10c8-7fdf-a7ec-f85248d2293c"),
                    CreatedAt = new DateTime(2001, 1, 1, 1, 1, 1, DateTimeKind.Utc),
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
                    Id = Guid.Parse("00e46784-34c8-7fdf-a7ec-f85248d2293c"),
                    CreatedAt = new DateTime(2001, 2, 1, 1, 1, 1, DateTimeKind.Utc),
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
                    Id = Guid.Parse("00ebbf35-60c8-7fdf-a7ec-f85248d2293c"),
                    CreatedAt = new DateTime(2002, 2, 1, 1, 1, 1, DateTimeKind.Utc),
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
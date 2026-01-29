using Altinn.ApiClients.Maskinporten.Config;
using Altinn.DialogportenAdapter.Unit.Tests.Common.Assert;
using Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;
using Altinn.DialogportenAdapter.WebApi;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Altinn.DialogportenAdapter.Unit.Tests.Features.Command.Sync;

public class StorageDialogportenDataMergerUpdateTest
{
    private readonly IRegisterRepository _registerRepositoryMock = Substitute.For<IRegisterRepository>();
    private readonly StorageDialogportenDataMerger _storageDialogportenDataMerger;
    private const string PartyId1 = "party-1";
    private const string PartyId2 = "party-2";
    private const int UserId1 = 1;
    private const int UserId2 = 2;
    private AdapterFeatureFlagSettings _featureFlags = new() { EnableSubmissionTransmissions = true };

    public StorageDialogportenDataMergerUpdateTest()
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

    /**
     * Generic test. Covers:
     * - IsApiOnly (mutable)
     * - Party (immutable)
     * - VisibleFrom (immutable)
     * - DueAt (mutable)
     * - CreatedAt (immutable)
     * - UpdatedAt (immutable)
     * - Content.Title
     * - Content.AdditionalInfo
     * - Content.ExtendedStatus
     */
    [Fact(DisplayName = "Given an existing Dialog and changes to immutable base properties, should ignore changes")]
    public async Task Merge_ExistingDialogWithChangesToImmutableProperties_ShouldIgnoreChanges()
    {
        var dialogId = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f");
        var mergeDto = new MergeDto(
            Application: AltinnApplicationBuilder
                .NewDefaultAltinnApplication()
                .WithMessageBoxConfig(new MessageBoxConfig
                {
                    HideSettings = new HideSettings { HideAlways = true },
                })
                .WithTitle(
                    new Dictionary<string, string>()
                    {
                        ["nb"] = "Test applikasjon oppdatert",
                        ["en"] = "Test application updated"
                    })
                .Build(),
            ApplicationTexts: new ApplicationTexts
            {
                Translations =
                [
                    new ApplicationTextsTranslation
                    {
                        Language = "nb",
                        Texts = new Dictionary<string, string>
                        {
                            ["dp.additionalinfo.element-id"] = "ekstrainfo"
                        }
                    }
                ]
            },
            DialogId: dialogId,
            Events: new InstanceEventList
            {
                InstanceEvents =
                [
                    AltinnInstanceEventBuilder.NewCreatedByPlatformUserInstanceEvent(UserId1)
                        .WithCreated(new DateTime(1004, 1, 1, 1, 1, 1, DateTimeKind.Utc))
                        .Build(),
                ]
            },
            Instance: AltinnInstanceBuilder
                .NewInProgressInstance()
                .WithDueBefore(new DateTime(9999, 1, 1, 1, 1, 1, DateTimeKind.Utc))
                .WithStatus(new InstanceStatus
                {
                    IsArchived = false,
                    Archived = null,
                    IsSoftDeleted = false,
                    SoftDeleted = null,
                    IsHardDeleted = false,
                    HardDeleted = null,
                    ReadStatus = ReadStatus.Unread,
                    Substatus = new Substatus
                    {
                        Label = "updated substatus",
                        Description = null
                    }
                })
                .WithInstanceOwner(new InstanceOwner
                {
                    PartyId = PartyId2,
                    PersonNumber = null,
                    OrganisationNumber = null,
                    Username = null
                })
                .WithProcess(new ProcessState
                {
                    Started = null,
                    StartEvent = null,
                    CurrentTask = new ProcessElementInfo
                    {
                        Flow = null,
                        Started = null,
                        ElementId = "element-id",
                        Name = null,
                        AltinnTaskType = null,
                        Ended = null,
                        FlowType = null
                    },
                    Ended = null,
                    EndEvent = null
                })
                .WithCreated(new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc))
                .WithLastChanged(new DateTime(1001, 1, 1, 1, 1, 1, DateTimeKind.Utc))
                .WithVisibleAfter(new DateTime(1002, 1, 1, 1, 1, 1, DateTimeKind.Utc))
                .WithDueBefore(new DateTime(5000, 1, 1, 1, 1, 1, DateTimeKind.Utc))
                .Build(),
            ExistingDialog: new DialogDto
            {
                Id = dialogId,
                IsApiOnly = false,
                Revision = null,
                ServiceResource = "urn:altinn:resource:app_appid",
                Party = "urn:actor.by.party.id",
                Progress = null,
                ExtendedStatus = null,
                ExternalReference = null,
                VisibleFrom = new DateTimeOffset(4000, 1, 1, 1, 1, 1, TimeSpan.Zero),
                DueAt = new DateTimeOffset(4001, 1, 1, 1, 1, 1, TimeSpan.Zero),
                Process = null,
                PrecedingProcess = null,
                ExpiresAt = null,
                CreatedAt = new DateTime(4002, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                UpdatedAt = new DateTime(4003, 1, 1, 1, 1, 2, DateTimeKind.Utc),
                Status = DialogStatus.InProgress,
                SystemLabel = SystemLabel.Default,
                ServiceOwnerContext = Regulars.ServiceOwnerContexts.DefaultContext,
                Content = Regulars.Content.ReadyForSubmission,
                SearchTags = [],
                Attachments = [],
                Transmissions = [],
                GuiActions =
                [
                    Regulars.GuiActions.Delete(dialogId),
                    Regulars.GuiActions.Write(dialogId)
                ],
                ApiActions = [],
                Activities = [],
                Deleted = false
            },
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = dialogId,
            IsApiOnly = true,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_appid",
            Party = "urn:actor.by.party.id",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = new DateTimeOffset(4000, 1, 1, 1, 1, 1, TimeSpan.Zero),
            DueAt = new DateTimeOffset(5000, 1, 1, 1, 1, 1, TimeSpan.Zero),
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(4002, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            UpdatedAt = new DateTime(4003, 1, 1, 1, 1, 2, DateTimeKind.Utc),
            Status = DialogStatus.InProgress,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = Regulars.ServiceOwnerContexts.DefaultContext,
            Content = new ContentDto
            {
                Summary = Regulars.Content.ReadyForSubmission.Summary,
                Title = new ContentValueDto
                {
                    Value =
                    [
                        new LocalizationDto { Value = "Test applikasjon oppdatert", LanguageCode = "nb" },
                        new LocalizationDto { Value = "Test application updated", LanguageCode = "en" },
                    ],
                    MediaType = "text/plain",
                },
                AdditionalInfo = new ContentValueDto
                {
                    Value =
                    [
                        new LocalizationDto { Value = "ekstrainfo", LanguageCode = "nb" },
                    ],
                    MediaType = "text/plain",
                },
                ExtendedStatus = new ContentValueDto
                {
                    Value =
                    [
                        new LocalizationDto { Value = "updated substatus", LanguageCode = "nb" },
                    ],
                    MediaType = "text/plain",
                },
            },
            SearchTags = [],
            Attachments = [],
            Transmissions = [],
            GuiActions =
            [
                Regulars.GuiActions.Delete(dialogId),
                Regulars.GuiActions.Write(dialogId, "urn:altinn:task:element-id"),
            ],
            ApiActions = [],
            Activities = [],
            Deleted = false
        });
    }

    /**
     * Covers:
     * - Status
     * - Content.Summary
     * - GuiActions
     */
    [Fact(DisplayName =
        "Given Status moving from InProgress -> Archived, should update Content.Summary and remove Gui Write Action")]
    public async Task Merge_StatusInprogressToArchived_ShouldUpdateContentSummaryAndRemoveTheGuiWriteAction()
    {
        var dialogId = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f");
        var mergeDto = new MergeDto(
            Application: AltinnApplicationBuilder
                .NewDefaultAltinnApplication()
                .Build(),
            ApplicationTexts: new ApplicationTexts { Translations = [] },
            DialogId: dialogId,
            Events: new InstanceEventList
            {
                InstanceEvents =
                [
                    AltinnInstanceEventBuilder.NewCreatedByPlatformUserInstanceEvent(UserId1)
                        .WithId(Guid.Parse("74994d35-00f0-4a8a-979a-bccdaca101b8"))
                        .WithCreated(new DateTime(2001, 1, 1, 1, 1, 1, DateTimeKind.Utc))
                        .Build(),
                    AltinnInstanceEventBuilder.NewSubmittedByPlatformUserInstanceEvent(UserId1)
                        .WithId(Guid.Parse("e157fd48-13a7-4fbc-996e-9a05cd175766"))
                        .WithCreated(new DateTime(2001, 2, 1, 1, 1, 1, DateTimeKind.Utc))
                        .WithUser(new PlatformUser { UserId = UserId1 })
                        .Build(),
                ]
            },
            Instance: AltinnInstanceBuilder
                .NewArchivedAltinnInstance()
                .Build(),
            ExistingDialog: new DialogDto
            {
                Id = dialogId,
                IsApiOnly = false,
                Revision = null,
                ServiceResource = "urn:altinn:resource:app_appid",
                Party = "urn:actor.by.party.id.party1",
                Progress = null,
                ExtendedStatus = null,
                ExternalReference = null,
                VisibleFrom = null,
                DueAt = null,
                Process = null,
                PrecedingProcess = null,
                ExpiresAt = null,
                CreatedAt = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2000, 1, 1, 1, 1, 2, DateTimeKind.Utc),
                Status = DialogStatus.InProgress,
                SystemLabel = SystemLabel.Default,
                ServiceOwnerContext = Regulars.ServiceOwnerContexts.DefaultContext,
                Content = Regulars.Content.ReadyForSubmission,
                SearchTags = [],
                Attachments = [],
                Transmissions = [],
                GuiActions =
                [
                    Regulars.GuiActions.Delete(dialogId),
                    Regulars.GuiActions.Write(dialogId),
                ],
                ApiActions = [],
                Activities = [],
                Deleted = false
            },
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = dialogId,
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_appid",
            Party = "urn:actor.by.party.id.party1",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = null,
            DueAt = new DateTimeOffset(9999, 1, 1, 1, 1, 1, TimeSpan.Zero),
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2000, 1, 1, 1, 1, 2, DateTimeKind.Utc),
            Status = DialogStatus.Completed,
            SystemLabel = SystemLabel.Default,
            ServiceOwnerContext = Regulars.ServiceOwnerContexts.DefaultContext,
            Content = Regulars.Content.Submitted,
            SearchTags = [],
            Attachments = [],
            Transmissions =
            [
                new TransmissionDto
                {
                    Id = Guid.Parse("00e46784-34c8-7fbc-996e-9a05cd175766"),
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
                        Regulars.Transmission.Attachment.Receipt(Guid.Parse("00e46784-34c8-7604-8854-78b655d2a377"))
                    ]
                },
            ],
            GuiActions = [Regulars.GuiActions.Delete(dialogId)],
            ApiActions = [],
            Activities =
            [
                new ActivityDto
                {
                    Id = Guid.Parse("00e46784-34c8-7fbc-996e-9a05cd175766"),
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
                }
            ],
            Deleted = false
        });
    }

    /**
     * Covers:
     * - Attachements
     * - Transmissions
     */
    [Fact(DisplayName =
        "Given an existing Dialog and changes to attachments and transmissions, should return expected MergeDto")]
    public async Task Merge_ExistingDialogAndChangesToAttachmentsAndTransmissions_ShouldReturnMergeDto()
    {
        var dialogId = Guid.Parse("902de1ba-6919-4355-99ad-7ad279266a2f");
        var mergeDto = new MergeDto(
            Application: AltinnApplicationBuilder
                .NewDefaultAltinnApplication()
                .WithDataTypes([
                    AltinnDataTypeBuilder.NewDefaultDataType().WithId("png-1").Build(),
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
            ApplicationTexts: new ApplicationTexts { Translations = [] },
            DialogId: dialogId,
            Events: new InstanceEventList
            {
                InstanceEvents =
                [
                    AltinnInstanceEventBuilder.NewCreatedByPlatformUserInstanceEvent(UserId1)
                        .Build(),
                    AltinnInstanceEventBuilder.NewSubmittedByPlatformUserInstanceEvent(UserId1)
                        .WithCreated(new DateTime(2001, 2, 1, 1, 1, 1, DateTimeKind.Utc))
                        .Build(),
                ]
            },
            Instance: AltinnInstanceBuilder
                .NewInProgressInstance()
                .WithDueBefore(new DateTime(9999, 1, 1, 1, 1, 1, DateTimeKind.Utc))
                .WithInstanceOwner(new InstanceOwner
                {
                    PartyId = PartyId2,
                    PersonNumber = null,
                    OrganisationNumber = null,
                    Username = null
                })
                .WithData([
                    AltinnDataElementBuilder
                        .NewDefaultDataElementBuilder()
                        .WithId("019bd57e-ce5e-74ed-8130-3a1ac8af3d91")
                        .WithLastChanged(new DateTime(2001, 1, 1, 1, 1, 1, DateTimeKind.Utc))
                        .WithFilename("outside-transmission")
                        .Build(),
                    AltinnDataElementBuilder
                        .NewDefaultDataElementBuilder()
                        .WithLastChanged(new DateTime(2001, 2, 1, 1, 1, 1, DateTimeKind.Utc))
                        .WithLastChangedBy("12345678912")
                        .WithId("019bd5eb-4239-7a40-a823-7735059ef136")
                        .WithFilename("in-transmission-as-1")
                        .Build(),
                ])
                .Build(),
            ExistingDialog: new DialogDto
            {
                Id = dialogId,
                IsApiOnly = false,
                Revision = null,
                ServiceResource = "urn:altinn:resource:app_appid",
                Party = "urn:actor.by.party.id",
                Progress = null,
                ExtendedStatus = null,
                ExternalReference = null,
                VisibleFrom = new DateTimeOffset(4000, 1, 1, 1, 1, 1, TimeSpan.Zero),
                DueAt = new DateTimeOffset(4001, 1, 1, 1, 1, 1, TimeSpan.Zero),
                Process = null,
                PrecedingProcess = null,
                ExpiresAt = null,
                CreatedAt = new DateTime(4002, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                UpdatedAt = new DateTime(4003, 1, 1, 1, 1, 2, DateTimeKind.Utc),
                Status = DialogStatus.InProgress,
                SystemLabel = SystemLabel.Default,
                ServiceOwnerContext = Regulars.ServiceOwnerContexts.DefaultContext,
                Content = Regulars.Content.ReadyForSubmission,
                SearchTags = [],
                Attachments = [],
                Transmissions = [],
                GuiActions =
                [
                    Regulars.GuiActions.Delete(dialogId),
                    Regulars.GuiActions.Write(dialogId)
                ],
                ApiActions = [],
                Activities = [],
                Deleted = false
            },
            IsMigration: false
        );

        var actualDialogDto = await _storageDialogportenDataMerger.Merge(mergeDto, CancellationToken.None);

        actualDialogDto.Should().BeEquivalentTo(new DialogDto
        {
            Id = dialogId,
            IsApiOnly = false,
            Revision = null,
            ServiceResource = "urn:altinn:resource:app_appid",
            Party = "urn:actor.by.party.id",
            Progress = null,
            ExtendedStatus = null,
            ExternalReference = null,
            VisibleFrom = new DateTimeOffset(4000, 1, 1, 1, 1, 1, TimeSpan.Zero),
            DueAt = new DateTimeOffset(9999, 1, 1, 1, 1, 1, TimeSpan.Zero),
            Process = null,
            PrecedingProcess = null,
            ExpiresAt = null,
            CreatedAt = new DateTime(4002, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            UpdatedAt = new DateTime(4003, 1, 1, 1, 1, 2, DateTimeKind.Utc),
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
                            MediaType = "image/jpg",
                            ConsumerType = AttachmentUrlConsumerType.Gui
                        }
                    ]
                }
            ],
            Transmissions =
            [
                new TransmissionDto
                {
                    Id = Guid.Parse("00e46784-34c8-7f4d-bed1-d5417461bb53"),
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
                            Id = Guid.Parse("00dc6b07-88c8-7cdd-8fae-97f63b3922d7"),
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
                                    MediaType = "image/jpg",
                                    ConsumerType = AttachmentUrlConsumerType.Gui
                                }
                            ]
                        },
                        Regulars.Transmission.Attachment.Receipt(Guid.Parse("00e46784-34c8-7d41-ae89-8734b51a97ab")),
                    ]
                },
            ],
            GuiActions =
            [
                Regulars.GuiActions.Delete(dialogId),
                Regulars.GuiActions.Write(dialogId),
            ],
            ApiActions = [],
            Activities =
            [
                new ActivityDto
                {
                    Id = Guid.Parse("00e46784-34c8-7f4d-bed1-d5417461bb53"),
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
                }
            ],
            Deleted = false
        });
    }
}
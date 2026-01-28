using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnApplicationBuilder(Application application)
{
    public static AltinnApplicationBuilder NewDefaultAltinnApplication()
    {
        return new AltinnApplicationBuilder
        (
            new Application
            {
                CreatedBy = "you",
                Created = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                LastChanged = new DateTime(1001, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                LastChangedBy = "another",
                Id = "application-id",
                VersionId = "1.0",
                Org = "123456789",
                Title = new Dictionary<string, string>
                {
                    ["nb"] = "Test applikasjon",
                    ["en"] = "Test application"
                },
                ValidFrom = new DateTime(1003, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                ValidTo = new DateTime(9999, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                ProcessId = "process-id",
                DataTypes = [],
                PartyTypesAllowed = new PartyTypesAllowed
                {
                    BankruptcyEstate = false,
                    Organisation = true,
                    Person = false,
                    SubUnit = false
                },
                AutoDeleteOnProcessEnd = false,
                PreventInstanceDeletionForDays = 1,
                PresentationFields =
                [
                    new DataField
                    {
                        Id = "presentation-field-id",
                        Path = "/path",
                        DataTypeId = "presentation-field-data-type-id"
                    }
                ],
                DataFields = [],
                EFormidling = new EFormidlingContract
                {
                    ServiceId = "service-id",
                    DPFShipmentType = "DPFShipmentType",
                    Receiver = "Receiver",
                    SendAfterTaskId = "1",
                    Process = "Process",
                    Standard = "Standard",
                    TypeVersion = "TypeVersion",
                    Type = "Type",
                    SecurityLevel = 0,
                    DataTypes = ["eformidling-data-type"]
                },
                OnEntry = new OnEntryConfig
                {
                    Show = "show"
                },
                MessageBoxConfig = new MessageBoxConfig
                {
                    HideSettings = new HideSettings
                    {
                        HideAlways = false,
                        HideOnTask = ["task-hide"]
                    },
                    SyncAdapterSettings = new SyncAdapterSettings
                    {
                        DisableSync = false,
                        DisableCreate = false,
                        DisableDelete = false,
                        DisableAddActivities = false,
                        DisableAddTransmissions = false,
                        DisableSyncDueAt = false,
                        DisableSyncStatus = false,
                        DisableSyncContentTitle = false,
                        DisableSyncContentSummary = false,
                        DisableSyncAttachments = false,
                        DisableSyncApiActions = false,
                        DisableSyncGuiActions = false
                    }
                },
                CopyInstanceSettings = new CopyInstanceSettings
                {
                    Enabled = false,
                    ExcludedDataTypes = ["exclude-type"],
                    ExcludedDataFields = ["exclude-field"],
                },
                StorageAccountNumber = null,
                DisallowUserInstantiation = false
            }
        );
    }

    public AltinnApplicationBuilder WithId(string id)
    {
        application.Id = id;
        return this;
    }

    public AltinnApplicationBuilder WithVersionId(string versionId)
    {
        application.VersionId = versionId;
        return this;
    }

    public AltinnApplicationBuilder WithOrg(string org)
    {
        application.Org = org;
        return this;
    }

    public AltinnApplicationBuilder WithTitle(Dictionary<string, string> title)
    {
        application.Title = title;
        return this;
    }

    public AltinnApplicationBuilder WithValidFrom(DateTime validFrom)
    {
        application.ValidFrom = validFrom;
        return this;
    }

    public AltinnApplicationBuilder WithValidTo(DateTime? validTo)
    {
        application.ValidTo = validTo;
        return this;
    }

    public AltinnApplicationBuilder WithProcessId(string processId)
    {
        application.ProcessId = processId;
        return this;
    }

    public AltinnApplicationBuilder WithDataTypes(params DataType[] dataTypes)
    {
        application.DataTypes = new List<DataType>(dataTypes);
        return this;
    }

    public AltinnApplicationBuilder WithPartyTypesAllowed(PartyTypesAllowed partyTypesAllowed)
    {
        application.PartyTypesAllowed = partyTypesAllowed;
        return this;
    }

    public AltinnApplicationBuilder AutoDeleteOnProcessEnd()
    {
        application.AutoDeleteOnProcessEnd = true;
        return this;
    }

    public AltinnApplicationBuilder PreventDeletionForDays(int days)
    {
        application.PreventInstanceDeletionForDays = days;
        return this;
    }

    public AltinnApplicationBuilder WithPresentationFields(params DataField[] fields)
    {
        application.PresentationFields = new List<DataField>(fields);
        return this;
    }

    public AltinnApplicationBuilder WithDataFields(params DataField[] fields)
    {
        application.DataFields = new List<DataField>(fields);
        return this;
    }

    public AltinnApplicationBuilder WithEFormidling(EFormidlingContract contract)
    {
        application.EFormidling = contract;
        return this;
    }

    public AltinnApplicationBuilder WithOnEntry(OnEntryConfig config)
    {
        application.OnEntry = config;
        return this;
    }

    public AltinnApplicationBuilder WithMessageBoxConfig(MessageBoxConfig config)
    {
        application.MessageBoxConfig = config;
        return this;
    }

    public AltinnApplicationBuilder WithCopyInstanceSettings(CopyInstanceSettings settings)
    {
        application.CopyInstanceSettings = settings;
        return this;
    }

    public AltinnApplicationBuilder WithStorageAccountNumber(int number)
    {
        application.StorageAccountNumber = number;
        return this;
    }

    public AltinnApplicationBuilder WithDisallowUserInstantiation(bool disallowUserInstantiation)
    {
        application.DisallowUserInstantiation = disallowUserInstantiation;
        return this;
    }

    public Application Build()
    {
        return new Application
        {
            Created = application.Created,
            CreatedBy = application.CreatedBy,
            LastChanged = application.LastChanged,
            LastChangedBy = application.LastChangedBy,
            Id = application.Id,
            VersionId = application.VersionId,
            Org = application.Org,
            Title = application.Title,
            ValidFrom = application.ValidFrom,
            ValidTo = application.ValidTo,
            ProcessId = application.ProcessId,
            DataTypes = application.DataTypes?.Select(d => new AltinnDataTypeBuilder(d).Build()).ToList(),
            PartyTypesAllowed = application.PartyTypesAllowed != null
                ? new PartyTypesAllowed
                {
                    BankruptcyEstate = application.PartyTypesAllowed.BankruptcyEstate,
                    Organisation = application.PartyTypesAllowed.Organisation,
                    Person = application.PartyTypesAllowed.Person,
                    SubUnit = application.PartyTypesAllowed.SubUnit
                }
                : null,
            AutoDeleteOnProcessEnd = application.AutoDeleteOnProcessEnd,
            PreventInstanceDeletionForDays = application.PreventInstanceDeletionForDays,
            PresentationFields = application.PresentationFields?.Select(p => new DataField
            {
                Id = p.Id,
                Path = p.Path,
                DataTypeId = p.DataTypeId
            }).ToList(),
            DataFields = application.DataFields?.Select(p => new DataField
            {
                Id = p.Id,
                Path = p.Path,
                DataTypeId = p.DataTypeId
            }).ToList(),
            EFormidling = application.EFormidling != null
                ? new EFormidlingContract
                {
                    ServiceId = application.EFormidling.ServiceId,
                    DPFShipmentType = application.EFormidling.DPFShipmentType,
                    Receiver = application.EFormidling.Receiver,
                    SendAfterTaskId = application.EFormidling.SendAfterTaskId,
                    Process = application.EFormidling.Process,
                    Standard = application.EFormidling.Standard,
                    TypeVersion = application.EFormidling.TypeVersion,
                    Type = application.EFormidling.Type,
                    SecurityLevel = application.EFormidling.SecurityLevel,
                    DataTypes = application.EFormidling.DataTypes?.ToList()
                }
                : null,
            OnEntry = application.OnEntry != null
                ? new OnEntryConfig
                {
                    Show = application.OnEntry.Show,
                }
                : null,
            MessageBoxConfig = application.MessageBoxConfig != null
                ? new MessageBoxConfig
                {
                    HideSettings = application.MessageBoxConfig.HideSettings,
                    SyncAdapterSettings = application.MessageBoxConfig.SyncAdapterSettings != null
                        ? new SyncAdapterSettings
                        {
                            DisableSync = application.MessageBoxConfig.SyncAdapterSettings.DisableSync,
                            DisableCreate = application.MessageBoxConfig.SyncAdapterSettings.DisableCreate,
                            DisableDelete = application.MessageBoxConfig.SyncAdapterSettings.DisableDelete,
                            DisableAddActivities =
                                application.MessageBoxConfig.SyncAdapterSettings.DisableAddActivities,
                            DisableAddTransmissions =
                                application.MessageBoxConfig.SyncAdapterSettings.DisableAddTransmissions,
                            DisableSyncDueAt = application.MessageBoxConfig.SyncAdapterSettings.DisableSyncDueAt,
                            DisableSyncStatus = application.MessageBoxConfig.SyncAdapterSettings.DisableSyncStatus,
                            DisableSyncContentTitle =
                                application.MessageBoxConfig.SyncAdapterSettings.DisableSyncContentTitle,
                            DisableSyncContentSummary = application.MessageBoxConfig.SyncAdapterSettings
                                .DisableSyncContentSummary,
                            DisableSyncAttachments =
                                application.MessageBoxConfig.SyncAdapterSettings.DisableSyncAttachments,
                            DisableSyncApiActions =
                                application.MessageBoxConfig.SyncAdapterSettings.DisableSyncApiActions,
                            DisableSyncGuiActions =
                                application.MessageBoxConfig.SyncAdapterSettings.DisableSyncGuiActions
                        }
                        : null,
                }
                : null,
            CopyInstanceSettings = application.CopyInstanceSettings != null
                ? new CopyInstanceSettings
                {
                    Enabled = application.CopyInstanceSettings.Enabled,
                    ExcludedDataTypes = application.CopyInstanceSettings.ExcludedDataTypes,
                    ExcludedDataFields = application.CopyInstanceSettings.ExcludedDataFields
                }
                : null,
            StorageAccountNumber = application.StorageAccountNumber,
            DisallowUserInstantiation = application.DisallowUserInstantiation,
        };
    }
}
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnApplicationBuilder
{
    private readonly Application _application;

    public AltinnApplicationBuilder(Application application)
    {
        _application = application;
    }

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
                        DataTypeId = "presentaiton-field-data-type-id"
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
        _application.Id = id;
        return this;
    }

    public AltinnApplicationBuilder WithVersionId(string versionId)
    {
        _application.VersionId = versionId;
        return this;
    }

    public AltinnApplicationBuilder WithOrg(string org)
    {
        _application.Org = org;
        return this;
    }

    public AltinnApplicationBuilder WithTitle(Dictionary<string, string> title)
    {
        _application.Title = title;
        return this;
    }

    public AltinnApplicationBuilder WithValidFrom(DateTime validFrom)
    {
        _application.ValidFrom = validFrom;
        return this;
    }

    public AltinnApplicationBuilder WithValidTo(DateTime? validTo)
    {
        _application.ValidTo = validTo;
        return this;
    }

    public AltinnApplicationBuilder WithProcessId(string processId)
    {
        _application.ProcessId = processId;
        return this;
    }

    public AltinnApplicationBuilder WithDataTypes(params DataType[] dataTypes)
    {
        _application.DataTypes = new List<DataType>(dataTypes);
        return this;
    }

    public AltinnApplicationBuilder WithPartyTypesAllowed(PartyTypesAllowed partyTypesAllowed)
    {
        _application.PartyTypesAllowed = partyTypesAllowed;
        return this;
    }

    public AltinnApplicationBuilder AutoDeleteOnProcessEnd()
    {
        _application.AutoDeleteOnProcessEnd = true;
        return this;
    }

    public AltinnApplicationBuilder PreventDeletionForDays(int days)
    {
        _application.PreventInstanceDeletionForDays = days;
        return this;
    }

    public AltinnApplicationBuilder WithPresentationFields(params DataField[] fields)
    {
        _application.PresentationFields = new List<DataField>(fields);
        return this;
    }

    public AltinnApplicationBuilder WithDataFields(params DataField[] fields)
    {
        _application.DataFields = new List<DataField>(fields);
        return this;
    }

    public AltinnApplicationBuilder WithEFormidling(EFormidlingContract contract)
    {
        _application.EFormidling = contract;
        return this;
    }

    public AltinnApplicationBuilder WithOnEntry(OnEntryConfig config)
    {
        _application.OnEntry = config;
        return this;
    }

    public AltinnApplicationBuilder WithMessageBoxConfig(MessageBoxConfig config)
    {
        _application.MessageBoxConfig = config;
        return this;
    }

    public AltinnApplicationBuilder WithCopyInstanceSettings(CopyInstanceSettings settings)
    {
        _application.CopyInstanceSettings = settings;
        return this;
    }

    public AltinnApplicationBuilder WithStorageAccountNumber(int number)
    {
        _application.StorageAccountNumber = number;
        return this;
    }

    public AltinnApplicationBuilder WithDisallowUserInstantiation(bool disallowUserInstantiation)
    {
        _application.DisallowUserInstantiation = disallowUserInstantiation;
        return this;
    }

    public Application Build()
    {
        return new Application
        {
            Created = _application.Created,
            CreatedBy = _application.CreatedBy,
            LastChanged = _application.LastChanged,
            LastChangedBy = _application.LastChangedBy,
            Id = _application.Id,
            VersionId = _application.VersionId,
            Org = _application.Org,
            Title = _application.Title,
            ValidFrom = _application.ValidFrom,
            ValidTo = _application.ValidTo,
            ProcessId = _application.ProcessId,
            DataTypes = _application.DataTypes?.Select(d => new AltinnDataTypeBuilder(d).Build()).ToList(),
            PartyTypesAllowed = _application.PartyTypesAllowed != null
                ? new PartyTypesAllowed
                {
                    BankruptcyEstate = _application.PartyTypesAllowed.BankruptcyEstate,
                    Organisation = _application.PartyTypesAllowed.Organisation,
                    Person = _application.PartyTypesAllowed.Person,
                    SubUnit = _application.PartyTypesAllowed.SubUnit
                }
                : null,
            AutoDeleteOnProcessEnd = _application.AutoDeleteOnProcessEnd,
            PreventInstanceDeletionForDays = _application.PreventInstanceDeletionForDays,
            PresentationFields = _application.PresentationFields?.Select(p => new DataField
            {
                Id = p.Id,
                Path = p.Path,
                DataTypeId = p.DataTypeId
            }).ToList(),
            DataFields = _application.DataFields?.Select(p => new DataField
            {
                Id = p.Id,
                Path = p.Path,
                DataTypeId = p.DataTypeId
            }).ToList(),
            EFormidling = _application.EFormidling != null
                ? new EFormidlingContract
                {
                    ServiceId = _application.EFormidling.ServiceId,
                    DPFShipmentType = _application.EFormidling.DPFShipmentType,
                    Receiver = _application.EFormidling.Receiver,
                    SendAfterTaskId = _application.EFormidling.SendAfterTaskId,
                    Process = _application.EFormidling.Process,
                    Standard = _application.EFormidling.Standard,
                    TypeVersion = _application.EFormidling.TypeVersion,
                    Type = _application.EFormidling.Type,
                    SecurityLevel = _application.EFormidling.SecurityLevel,
                    DataTypes = _application.EFormidling.DataTypes?.ToList()
                }
                : null,
            OnEntry = _application.OnEntry != null
                ? new OnEntryConfig
                {
                    Show = _application.OnEntry.Show,
                }
                : null,
            MessageBoxConfig = _application.MessageBoxConfig != null
                ? new MessageBoxConfig
                {
                    HideSettings = _application.MessageBoxConfig.HideSettings,
                    SyncAdapterSettings = _application.MessageBoxConfig.SyncAdapterSettings != null
                        ? new SyncAdapterSettings
                        {
                            DisableSync = _application.MessageBoxConfig.SyncAdapterSettings.DisableSync,
                            DisableCreate = _application.MessageBoxConfig.SyncAdapterSettings.DisableCreate,
                            DisableDelete = _application.MessageBoxConfig.SyncAdapterSettings.DisableDelete,
                            DisableAddActivities =
                                _application.MessageBoxConfig.SyncAdapterSettings.DisableAddActivities,
                            DisableAddTransmissions =
                                _application.MessageBoxConfig.SyncAdapterSettings.DisableAddTransmissions,
                            DisableSyncDueAt = _application.MessageBoxConfig.SyncAdapterSettings.DisableSyncDueAt,
                            DisableSyncStatus = _application.MessageBoxConfig.SyncAdapterSettings.DisableSyncStatus,
                            DisableSyncContentTitle =
                                _application.MessageBoxConfig.SyncAdapterSettings.DisableSyncContentTitle,
                            DisableSyncContentSummary = _application.MessageBoxConfig.SyncAdapterSettings
                                .DisableSyncContentSummary,
                            DisableSyncAttachments =
                                _application.MessageBoxConfig.SyncAdapterSettings.DisableSyncAttachments,
                            DisableSyncApiActions =
                                _application.MessageBoxConfig.SyncAdapterSettings.DisableSyncApiActions,
                            DisableSyncGuiActions =
                                _application.MessageBoxConfig.SyncAdapterSettings.DisableSyncGuiActions
                        }
                        : null,
                }
                : null,
            CopyInstanceSettings = _application.CopyInstanceSettings != null
                ? new CopyInstanceSettings
                {
                    Enabled = _application.CopyInstanceSettings.Enabled,
                    ExcludedDataTypes = _application.CopyInstanceSettings.ExcludedDataTypes,
                    ExcludedDataFields = _application.CopyInstanceSettings.ExcludedDataFields
                }
                : null,
            StorageAccountNumber = _application.StorageAccountNumber,
            DisallowUserInstantiation = _application.DisallowUserInstantiation,
        };
    }
}
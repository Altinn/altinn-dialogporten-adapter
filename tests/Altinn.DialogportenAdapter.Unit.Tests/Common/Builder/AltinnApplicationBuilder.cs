using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnApplicationBuilder
{
    private DateTime? _created = new(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc);
    private string? _createdBy = "me";
    private DateTime _lastChanged = new(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc);
    private string _lastChangedBy = "you";
    private string _id = "test/app-1";
    private string _versionId = "1.0";
    private string _org = "test";

    private Dictionary<string, string> _title = new()
    {
        ["nb"] = "Test applikasjon",
        ["en"] = "Test application"
    };

    private DateTime? _validFrom;
    private DateTime? _validTo;
    private string _processId = "process-1";
    private List<DataType> _dataTypes = new();
    private PartyTypesAllowed _partyTypesAllowed = new() { };
    private bool _autoDeleteOnProcessEnd = false;
    private int? _preventInstanceDeletionForDays = null;
    private List<DataField> _presentationFields = [];
    private List<DataField> _dataFields = [];

    private EFormidlingContract _eFormidling = new()
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
    };

    private OnEntryConfig _onEntry = new()
    {
        Show = "show"
    };

    private MessageBoxConfig _messageBoxConfig = new()
    {
        HideSettings = new HideSettings
        {
            HideAlways = true,
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
    };

    private CopyInstanceSettings _copyInstanceSettings = new()
    {
        Enabled = false,
        ExcludedDataTypes = ["exclude-type"],
        ExcludedDataFields = ["exclude-field"],
    };

    private int? _storageAccountNumber = null;
    private bool _disallowUserInstantiation = false;

    private AltinnApplicationBuilder()
    {
    }

    public static AltinnApplicationBuilder NewDefaultAltinnApplication()
    {
        return new AltinnApplicationBuilder
        {
            _createdBy = "you",
            _created = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            _lastChanged = new DateTime(1001, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            _lastChangedBy = "another",
            _id = "application-id",
            _versionId = "1.0",
            _org = "123456789",
            _title = new Dictionary<string, string>
            {
                ["nb"] = "Test applikasjon",
                ["en"] = "Test application"
            },
            _validFrom = new DateTime(1003, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            _validTo = new DateTime(9999, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            _processId = "process-id",
            _dataTypes = [],
            _partyTypesAllowed = new PartyTypesAllowed
            {
                BankruptcyEstate = false,
                Organisation = true,
                Person = false,
                SubUnit = false
            },
            _autoDeleteOnProcessEnd = false,
            _preventInstanceDeletionForDays = 1,
            _presentationFields = [
                new DataField
                {
                    Id = "presentation-field-id",
                    Path = "/path",
                    DataTypeId = "presentaiton-field-data-type-id"
                }
            ],
            _dataFields = [],
            _eFormidling = new EFormidlingContract
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
            _onEntry = new OnEntryConfig
            {
                Show = "show"
            },
            _messageBoxConfig = new MessageBoxConfig
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
            _copyInstanceSettings = new CopyInstanceSettings
            {
                Enabled = false,
                ExcludedDataTypes = ["exclude-type"],
                ExcludedDataFields = ["exclude-field"],
            },
            _storageAccountNumber = null,
            _disallowUserInstantiation = false
        };
    }

    public AltinnApplicationBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public AltinnApplicationBuilder WithVersionId(string versionId)
    {
        _versionId = versionId;
        return this;
    }

    public AltinnApplicationBuilder WithOrg(string org)
    {
        _org = org;
        return this;
    }

    public AltinnApplicationBuilder WithTitle(Dictionary<string, string> title)
    {
        _title = title;
        return this;
    }

    public AltinnApplicationBuilder WithValidFrom(DateTime validFrom)
    {
        _validFrom = validFrom;
        return this;
    }

    public AltinnApplicationBuilder WithValidTo(DateTime? validTo)
    {
        _validTo = validTo;
        return this;
    }

    public AltinnApplicationBuilder WithProcessId(string processId)
    {
        _processId = processId;
        return this;
    }

    public AltinnApplicationBuilder WithDataTypes(params DataType[] dataTypes)
    {
        _dataTypes = new List<DataType>(dataTypes);
        return this;
    }

    public AltinnApplicationBuilder WithPartyTypesAllowed(PartyTypesAllowed partyTypesAllowed)
    {
        _partyTypesAllowed = partyTypesAllowed;
        return this;
    }

    public AltinnApplicationBuilder AutoDeleteOnProcessEnd()
    {
        _autoDeleteOnProcessEnd = true;
        return this;
    }

    public AltinnApplicationBuilder PreventDeletionForDays(int days)
    {
        _preventInstanceDeletionForDays = days;
        return this;
    }

    public AltinnApplicationBuilder WithPresentationFields(params DataField[] fields)
    {
        _presentationFields = new List<DataField>(fields);
        return this;
    }

    public AltinnApplicationBuilder WithDataFields(params DataField[] fields)
    {
        _dataFields = new List<DataField>(fields);
        return this;
    }

    public AltinnApplicationBuilder WithEFormidling(EFormidlingContract contract)
    {
        _eFormidling = contract;
        return this;
    }

    public AltinnApplicationBuilder WithOnEntry(OnEntryConfig config)
    {
        _onEntry = config;
        return this;
    }

    public AltinnApplicationBuilder WithMessageBoxConfig(MessageBoxConfig config)
    {
        _messageBoxConfig = config;
        return this;
    }

    public AltinnApplicationBuilder WithCopyInstanceSettings(CopyInstanceSettings settings)
    {
        _copyInstanceSettings = settings;
        return this;
    }

    public AltinnApplicationBuilder WithStorageAccountNumber(int number)
    {
        _storageAccountNumber = number;
        return this;
    }

    public AltinnApplicationBuilder WithDisallowUserInstantiation(bool disallowUserInstantiation)
    {
        _disallowUserInstantiation = disallowUserInstantiation;
        return this;
    }

    public Application Build()
    {
        return new Application
        {
            Created = _created,
            CreatedBy = _createdBy,
            LastChanged = _lastChanged,
            LastChangedBy = _lastChangedBy,
            Id = _id,
            VersionId = _versionId,
            Org = _org,
            Title = _title,
            ValidFrom = _validFrom,
            ValidTo = _validTo,
            ProcessId = _processId,
            DataTypes = _dataTypes,
            PartyTypesAllowed = _partyTypesAllowed,
            AutoDeleteOnProcessEnd = _autoDeleteOnProcessEnd,
            PreventInstanceDeletionForDays = _preventInstanceDeletionForDays,
            PresentationFields = _presentationFields,
            DataFields = _dataFields,
            EFormidling = _eFormidling,
            OnEntry = _onEntry,
            MessageBoxConfig = _messageBoxConfig,
            CopyInstanceSettings = _copyInstanceSettings,
            StorageAccountNumber = _storageAccountNumber,
            DisallowUserInstantiation = _disallowUserInstantiation,
        };
    }
}
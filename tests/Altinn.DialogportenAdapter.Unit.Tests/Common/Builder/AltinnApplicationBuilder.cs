using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnApplicationBuilder
{
    private readonly Application _application;

    private AltinnApplicationBuilder(Application application)
    {
        _application = application;
    }

    public static AltinnApplicationBuilder From(Application application) => new(application.DeepClone());

    public static AltinnApplicationBuilder NewDefaultAltinnApplication() => new(new Application
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
    });

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
        _application.DataTypes = [.. dataTypes];
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
        _application.PresentationFields = [.. fields];
        return this;
    }

    public AltinnApplicationBuilder WithDataFields(params DataField[] fields)
    {
        _application.DataFields = [.. fields];
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
        return _application.DeepClone();
    }
}
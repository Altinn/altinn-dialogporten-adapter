using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnInstanceBuilder
{
    private DateTime? _created = new(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc);
    private string? _createdBy = "me";
    private DateTime _lastChanged = new(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc);
    private string _lastChangedBy = "you";
    private string _id;
    private InstanceOwner _instanceOwner;
    private string _appId;
    private string _org;
    private ResourceLinks? _selfLinks;
    private DateTime? _dueBefore;
    private DateTime? _visibleAfter;
    private ProcessState _process;
    private InstanceStatus _status;
    private List<CompleteConfirmation> _completeConfirmations = new List<CompleteConfirmation>();
    private List<DataElement> _data = new List<DataElement>();
    private Dictionary<string, string> _presentationTexts = new Dictionary<string, string>();
    private Dictionary<string, string> _dataValues = new Dictionary<string, string>();

    private AltinnInstanceBuilder()
    {
    }

    public static AltinnInstanceBuilder NewInProgressInstance()
    {
        return new AltinnInstanceBuilder
        {
            _created = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            _createdBy = "me",
            _lastChanged = new DateTime(1000, 2, 1, 1, 1, 1, DateTimeKind.Utc),
            _lastChangedBy = "you",
            _id = "instance-id",
            _instanceOwner = new InstanceOwner
            {
                PartyId = "party-1",
            },
            _appId = "urn:altinn:instance-id",
            _org = "org",
            _selfLinks = null,
            _dueBefore = null,
            _visibleAfter = null,
            _process = new ProcessState(),
            _status = new InstanceStatus(),
            _completeConfirmations =
            [
                new CompleteConfirmation()
            ],
            _data = [],
            _presentationTexts = new Dictionary<string, string>(),
            _dataValues = new Dictionary<string, string>()
        };
    }

    public static AltinnInstanceBuilder NewArchivedAltinnInstance()
    {
        return new AltinnInstanceBuilder
        {
            _created = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            _createdBy = "me",
            _lastChanged = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            _lastChangedBy = "you",
            _id = "instance-id",
            _instanceOwner = new InstanceOwner
            {
                PartyId = "party-1",
            },
            _appId = "application-id",
            _org = "org",
            _selfLinks = null,
            _dueBefore = new DateTime(9999, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            _visibleAfter = null,
            _process = new ProcessState
            {
                Started = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                StartEvent = null,
                CurrentTask = new ProcessElementInfo
                {
                    Flow = null,
                    Started = null,
                    ElementId = null,
                    Name = null,
                    AltinnTaskType = "archived",
                    Ended = null,
                    FlowType = null
                },
                Ended = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                EndEvent = null
            },
            _status = new InstanceStatus
            {
                IsArchived = true,
                Substatus = null
            },
            _completeConfirmations =
            [
                new CompleteConfirmation()
            ],
            _data = [],
            _presentationTexts = new Dictionary<string, string>(),
            _dataValues = new Dictionary<string, string>()
        };
    }

    public AltinnInstanceBuilder WithCreated(DateTime created)
    {
        _created = created;
        return this;
    }

    public AltinnInstanceBuilder WithCreatedBy(string createdBy)
    {
        _createdBy = createdBy;
        return this;
    }

    public AltinnInstanceBuilder WithLastChanged(DateTime lastChanged)
    {
        _lastChanged = lastChanged;
        return this;
    }

    public AltinnInstanceBuilder WithLastChangedBy(string lastChangedBy)
    {
        _lastChangedBy = lastChangedBy;
        return this;
    }

    public AltinnInstanceBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public AltinnInstanceBuilder WithInstanceOwner(InstanceOwner owner)
    {
        _instanceOwner = owner;
        return this;
    }

    public AltinnInstanceBuilder WithAppId(string appId)
    {
        _appId = appId;
        return this;
    }

    public AltinnInstanceBuilder WithOrg(string org)
    {
        _org = org;
        return this;
    }

    public AltinnInstanceBuilder WithSelfLinks(ResourceLinks links)
    {
        _selfLinks = links;
        return this;
    }

    public AltinnInstanceBuilder WithDueBefore(DateTime? dueBefore)
    {
        _dueBefore = dueBefore;
        return this;
    }

    public AltinnInstanceBuilder WithVisibleAfter(DateTime? visibleAfter)
    {
        _visibleAfter = visibleAfter;
        return this;
    }

    public AltinnInstanceBuilder WithProcess(ProcessState process)
    {
        _process = process;
        return this;
    }

    public AltinnInstanceBuilder WithStatus(InstanceStatus status)
    {
        _status = status;
        return this;
    }

    public AltinnInstanceBuilder WithData(List<DataElement> data)
    {
        _data = data;
        return this;
    }

    public AltinnInstanceBuilder WithCompleteConfirmations(List<CompleteConfirmation> completeConfirmations)
    {
        _completeConfirmations = completeConfirmations;
        return this;
    }

    public Instance Build()
    {
        return new Instance
        {
            Created = _created,
            CreatedBy = _createdBy,
            LastChanged = _lastChanged,
            LastChangedBy = _lastChangedBy,
            Id = _id,
            InstanceOwner = _instanceOwner,
            AppId = _appId,
            Org = _org,
            SelfLinks = _selfLinks,
            DueBefore = _dueBefore,
            VisibleAfter = _visibleAfter,
            Process = _process,
            Status = _status,
            CompleteConfirmations = _completeConfirmations,
            Data = _data,
            PresentationTexts = _presentationTexts,
            DataValues = _dataValues,
        };
    }
}
using Altinn.Platform.Storage.Interface.Enums;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnInstanceBuilder
{
    private readonly Instance _instance;

    private AltinnInstanceBuilder(Instance instance)
    {
        _instance = instance;
    }

    public static AltinnInstanceBuilder NewInProgressInstance()
    {
        return new AltinnInstanceBuilder
        (
            new Instance
            {
                Created = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                CreatedBy = "me",
                LastChanged = new DateTime(1000, 2, 1, 1, 1, 1, DateTimeKind.Utc),
                LastChangedBy = "you",
                Id = "instance-id",
                InstanceOwner = new InstanceOwner
                {
                    PartyId = "party-1",
                },
                AppId = "urn:altinn:instance-id",
                Org = "org",
                SelfLinks = null,
                DueBefore = null,
                VisibleAfter = null,
                Process = new ProcessState(),
                Status = new InstanceStatus(),
                CompleteConfirmations =
                [
                    new CompleteConfirmation()
                ],
                Data = [],
                PresentationTexts = new Dictionary<string, string>(),
                DataValues = new Dictionary<string, string>()
            });
    }

    public static AltinnInstanceBuilder NewArchivedAltinnInstance()
    {
        return new AltinnInstanceBuilder
        (new Instance
        {
            Created = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            CreatedBy = "me",
            LastChanged = new DateTime(1000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            LastChangedBy = "you",
            Id = "instance-id",
            InstanceOwner = new InstanceOwner
            {
                PartyId = "party-1",
            },
            AppId = "application-id",
            Org = "org",
            SelfLinks = null,
            DueBefore = new DateTime(9999, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            VisibleAfter = null,
            Process = new ProcessState
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
            Status = new InstanceStatus
            {
                IsArchived = true,
                Substatus = null
            },
            CompleteConfirmations =
            [
                new CompleteConfirmation()
            ],
            Data = [],
            PresentationTexts = new Dictionary<string, string>(),
            DataValues = new Dictionary<string, string>()
        });
    }

    public AltinnInstanceBuilder WithCreated(DateTime created)
    {
        _instance.Created = created;
        return this;
    }

    public AltinnInstanceBuilder WithCreatedBy(string createdBy)
    {
        _instance.CreatedBy = createdBy;
        return this;
    }

    public AltinnInstanceBuilder WithLastChanged(DateTime lastChanged)
    {
        _instance.LastChanged = lastChanged;
        return this;
    }

    public AltinnInstanceBuilder WithLastChangedBy(string lastChangedBy)
    {
        _instance.LastChangedBy = lastChangedBy;
        return this;
    }

    public AltinnInstanceBuilder WithId(string id)
    {
        _instance.Id = id;
        return this;
    }

    public AltinnInstanceBuilder WithInstanceOwner(InstanceOwner owner)
    {
        _instance.InstanceOwner = owner;
        return this;
    }

    public AltinnInstanceBuilder WithAppId(string appId)
    {
        _instance.AppId = appId;
        return this;
    }

    public AltinnInstanceBuilder WithOrg(string org)
    {
        _instance.Org = org;
        return this;
    }

    public AltinnInstanceBuilder WithSelfLinks(ResourceLinks links)
    {
        _instance.SelfLinks = links;
        return this;
    }

    public AltinnInstanceBuilder WithDueBefore(DateTime? dueBefore)
    {
        _instance.DueBefore = dueBefore;
        return this;
    }

    public AltinnInstanceBuilder WithVisibleAfter(DateTime? visibleAfter)
    {
        _instance.VisibleAfter = visibleAfter;
        return this;
    }

    public AltinnInstanceBuilder WithProcess(ProcessState process)
    {
        _instance.Process = process;
        return this;
    }

    public AltinnInstanceBuilder WithStatus(InstanceStatus status)
    {
        _instance.Status = status;
        return this;
    }

    public AltinnInstanceBuilder WithData(List<DataElement> data)
    {
        _instance.Data = data;
        return this;
    }

    public AltinnInstanceBuilder WithCompleteConfirmations(List<CompleteConfirmation> completeConfirmations)
    {
        _instance.CompleteConfirmations = completeConfirmations;
        return this;
    }

    public Instance Build()
    {
        return new Instance
        {
            Created = _instance.Created,
            CreatedBy = _instance.CreatedBy,
            LastChanged = _instance.LastChanged,
            LastChangedBy = _instance.LastChangedBy,
            Id = _instance.Id,
            InstanceOwner = _instance.InstanceOwner != null
                ? new InstanceOwner
                {
                    PartyId = _instance.InstanceOwner.PartyId,
                    PersonNumber = _instance.InstanceOwner.PersonNumber,
                    OrganisationNumber = _instance.InstanceOwner.OrganisationNumber,
                    Username = _instance.InstanceOwner.Username
                }
                : null,
            AppId = _instance.AppId,
            Org = _instance.Org,
            SelfLinks = _instance.SelfLinks != null
                ? new ResourceLinks
                {
                    Apps = _instance.SelfLinks.Apps,
                    Platform = _instance.SelfLinks.Platform,
                }
                : null,
            DueBefore = _instance.DueBefore,
            VisibleAfter = _instance.VisibleAfter,
            Process = _instance.Process != null
                ? new ProcessState
                {
                    Started = _instance.Process.Started,
                    StartEvent = _instance.Process.StartEvent,
                    CurrentTask = _instance.Process.CurrentTask != null
                        ? new ProcessElementInfo
                        {
                            Flow = _instance.Process.CurrentTask.Flow,
                            Started = _instance.Process.CurrentTask.Started,
                            ElementId = _instance.Process.CurrentTask.ElementId,
                            Name = _instance.Process.CurrentTask.Name,
                            AltinnTaskType = _instance.Process.CurrentTask.AltinnTaskType,
                            Ended = _instance.Process.CurrentTask.Ended,
                            FlowType = _instance.Process.CurrentTask.FlowType
                        }
                        : null,
                    Ended = _instance.Process.Ended,
                    EndEvent = _instance.Process.EndEvent
                }
                : null,
            Status = _instance.Status != null
                ? new InstanceStatus
                {
                    IsArchived = _instance.Status.IsArchived,
                    Archived = _instance.Status.Archived,
                    IsSoftDeleted = _instance.Status.IsSoftDeleted,
                    SoftDeleted = _instance.Status.SoftDeleted,
                    IsHardDeleted = _instance.Status.IsHardDeleted,
                    HardDeleted = _instance.Status.HardDeleted,
                    ReadStatus = _instance.Status.ReadStatus,
                    Substatus = _instance.Status.Substatus != null
                        ? new Substatus
                        {
                            Label = _instance.Status.Substatus.Label,
                            Description = _instance.Status.Substatus.Description,
                        }
                        : null
                }
                : null,
            CompleteConfirmations = _instance.CompleteConfirmations?.Select(cc => new CompleteConfirmation
            {
                StakeholderId = cc.StakeholderId,
                ConfirmedOn = cc.ConfirmedOn
            }).ToList(),
            Data = _instance.Data?.Select(d => new AltinnDataElementBuilder(d).Build()).ToList(),
            PresentationTexts = _instance.PresentationTexts?.ToDictionary(k => k.Key, v => v.Value),
            DataValues = _instance.DataValues?.ToDictionary(k => k.Key, v => v.Value),
        };
    }
}
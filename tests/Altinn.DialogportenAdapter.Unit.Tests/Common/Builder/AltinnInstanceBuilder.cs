using Altinn.Platform.Storage.Interface.Enums;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnInstanceBuilder(Instance instance)
{
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
        instance.Created = created;
        return this;
    }

    public AltinnInstanceBuilder WithCreatedBy(string createdBy)
    {
        instance.CreatedBy = createdBy;
        return this;
    }

    public AltinnInstanceBuilder WithLastChanged(DateTime lastChanged)
    {
        instance.LastChanged = lastChanged;
        return this;
    }

    public AltinnInstanceBuilder WithLastChangedBy(string lastChangedBy)
    {
        instance.LastChangedBy = lastChangedBy;
        return this;
    }

    public AltinnInstanceBuilder WithId(string id)
    {
        instance.Id = id;
        return this;
    }

    public AltinnInstanceBuilder WithInstanceOwner(InstanceOwner owner)
    {
        instance.InstanceOwner = owner;
        return this;
    }

    public AltinnInstanceBuilder WithAppId(string appId)
    {
        instance.AppId = appId;
        return this;
    }

    public AltinnInstanceBuilder WithOrg(string org)
    {
        instance.Org = org;
        return this;
    }

    public AltinnInstanceBuilder WithSelfLinks(ResourceLinks links)
    {
        instance.SelfLinks = links;
        return this;
    }

    public AltinnInstanceBuilder WithDueBefore(DateTime? dueBefore)
    {
        instance.DueBefore = dueBefore;
        return this;
    }

    public AltinnInstanceBuilder WithVisibleAfter(DateTime? visibleAfter)
    {
        instance.VisibleAfter = visibleAfter;
        return this;
    }

    public AltinnInstanceBuilder WithProcess(ProcessState process)
    {
        instance.Process = process;
        return this;
    }

    public AltinnInstanceBuilder WithStatus(InstanceStatus status)
    {
        instance.Status = status;
        return this;
    }

    public AltinnInstanceBuilder WithData(List<DataElement> data)
    {
        instance.Data = data;
        return this;
    }

    public AltinnInstanceBuilder WithCompleteConfirmations(List<CompleteConfirmation> completeConfirmations)
    {
        instance.CompleteConfirmations = completeConfirmations;
        return this;
    }

    public Instance Build()
    {
        return new Instance
        {
            Created = instance.Created,
            CreatedBy = instance.CreatedBy,
            LastChanged = instance.LastChanged,
            LastChangedBy = instance.LastChangedBy,
            Id = instance.Id,
            InstanceOwner = instance.InstanceOwner != null
                ? new InstanceOwner
                {
                    PartyId = instance.InstanceOwner.PartyId,
                    PersonNumber = instance.InstanceOwner.PersonNumber,
                    OrganisationNumber = instance.InstanceOwner.OrganisationNumber,
                    Username = instance.InstanceOwner.Username
                }
                : null,
            AppId = instance.AppId,
            Org = instance.Org,
            SelfLinks = instance.SelfLinks != null
                ? new ResourceLinks
                {
                    Apps = instance.SelfLinks.Apps,
                    Platform = instance.SelfLinks.Platform,
                }
                : null,
            DueBefore = instance.DueBefore,
            VisibleAfter = instance.VisibleAfter,
            Process = instance.Process != null
                ? new ProcessState
                {
                    Started = instance.Process.Started,
                    StartEvent = instance.Process.StartEvent,
                    CurrentTask = instance.Process.CurrentTask != null
                        ? new ProcessElementInfo
                        {
                            Flow = instance.Process.CurrentTask.Flow,
                            Started = instance.Process.CurrentTask.Started,
                            ElementId = instance.Process.CurrentTask.ElementId,
                            Name = instance.Process.CurrentTask.Name,
                            AltinnTaskType = instance.Process.CurrentTask.AltinnTaskType,
                            Ended = instance.Process.CurrentTask.Ended,
                            FlowType = instance.Process.CurrentTask.FlowType
                        }
                        : null,
                    Ended = instance.Process.Ended,
                    EndEvent = instance.Process.EndEvent
                }
                : null,
            Status = instance.Status != null
                ? new InstanceStatus
                {
                    IsArchived = instance.Status.IsArchived,
                    Archived = instance.Status.Archived,
                    IsSoftDeleted = instance.Status.IsSoftDeleted,
                    SoftDeleted = instance.Status.SoftDeleted,
                    IsHardDeleted = instance.Status.IsHardDeleted,
                    HardDeleted = instance.Status.HardDeleted,
                    ReadStatus = instance.Status.ReadStatus,
                    Substatus = instance.Status.Substatus != null
                        ? new Substatus
                        {
                            Label = instance.Status.Substatus.Label,
                            Description = instance.Status.Substatus.Description,
                        }
                        : null
                }
                : null,
            CompleteConfirmations = instance.CompleteConfirmations?.Select(cc => new CompleteConfirmation
            {
                StakeholderId = cc.StakeholderId,
                ConfirmedOn = cc.ConfirmedOn
            }).ToList(),
            Data = instance.Data?.Select(d => new AltinnDataElementBuilder(d).Build()).ToList(),
            PresentationTexts = instance.PresentationTexts?.ToDictionary(k => k.Key, v => v.Value),
            DataValues = instance.DataValues?.ToDictionary(k => k.Key, v => v.Value),
        };
    }
}
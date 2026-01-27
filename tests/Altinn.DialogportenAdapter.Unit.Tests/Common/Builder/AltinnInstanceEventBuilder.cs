using Altinn.Platform.Storage.Interface.Enums;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnInstanceEventBuilder(InstanceEvent instanceEvent)
{
    public static AltinnInstanceEventBuilder NewCreatedByPlatformUserInstanceEvent(int userId) =>
        new(
            new InstanceEvent
            {
                Id = Guid.Parse("7488c4f3-1012-4487-b575-d38b01688da0"),
                InstanceId = "instance-id",
                DataId = "data-id",
                Created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                EventType = nameof(InstanceEventType.Created),
                InstanceOwnerPartyId = "instance-owner-party-id",
                User = new PlatformUser
                {
                    UserId = userId,
                    OrgId = "org",
                    AuthenticationLevel = 0,
                    EndUserSystemId = null,
                    NationalIdentityNumber = null,
                    SystemUserId = null,
                    SystemUserOwnerOrgNo = null,
                    SystemUserName = null
                },
                RelatedUser = null,
                ProcessInfo = null,
                AdditionalInfo = null
            });

    public static AltinnInstanceEventBuilder NewSubmittedByPlatformUserInstanceEvent(int userId) =>
        new(
            new InstanceEvent
            {
                Id = Guid.Parse("019bfa70-c9f7-7f4d-bed1-d5417461bb53"),
                InstanceId = "instance-id",
                DataId = "data-id",
                Created = new DateTime(2001, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                EventType = nameof(InstanceEventType.Submited),
                InstanceOwnerPartyId = "instance-owner-party-id",
                User = new PlatformUser
                {
                    UserId = userId,
                    OrgId = "org",
                    AuthenticationLevel = 0,
                    EndUserSystemId = null,
                    NationalIdentityNumber = null,
                    SystemUserId = null,
                    SystemUserOwnerOrgNo = null,
                    SystemUserName = null
                },
                RelatedUser = null,
                ProcessInfo = null,
                AdditionalInfo = null
            });

    public AltinnInstanceEventBuilder WithId(Guid id)
    {
        instanceEvent.Id = id;
        return this;
    }

    public AltinnInstanceEventBuilder WithInstanceId(string instanceId)
    {
        instanceEvent.InstanceId = instanceId;
        return this;
    }

    public AltinnInstanceEventBuilder WithDataId(string dataId)
    {
        instanceEvent.DataId = dataId;
        return this;
    }

    public AltinnInstanceEventBuilder WithCreated(DateTime created)
    {
        instanceEvent.Created = created;
        return this;
    }

    public AltinnInstanceEventBuilder WithEventType(string eventType)
    {
        instanceEvent.EventType = eventType;
        return this;
    }

    public AltinnInstanceEventBuilder WithInstanceOwnerPartyId(string partyId)
    {
        instanceEvent.InstanceOwnerPartyId = partyId;
        return this;
    }

    public AltinnInstanceEventBuilder WithUser(PlatformUser user)
    {
        instanceEvent.User = user;
        return this;
    }

    public AltinnInstanceEventBuilder WithRelatedUser(PlatformUser relatedUser)
    {
        instanceEvent.RelatedUser = relatedUser;
        return this;
    }

    public AltinnInstanceEventBuilder WithProcessInfo(ProcessState processInfo)
    {
        instanceEvent.ProcessInfo = processInfo;
        return this;
    }

    public AltinnInstanceEventBuilder WithAdditionalInfo(string info)
    {
        instanceEvent.AdditionalInfo = info;
        return this;
    }

    public InstanceEvent Build()
    {
        return new InstanceEvent
        {
            Id = instanceEvent.Id,
            InstanceId = instanceEvent.InstanceId,
            DataId = instanceEvent.DataId,
            Created = instanceEvent.Created,
            EventType = instanceEvent.EventType,
            InstanceOwnerPartyId = instanceEvent.InstanceOwnerPartyId,
            User = instanceEvent.User != null
                ? new PlatformUser
                {
                    UserId = instanceEvent.User.UserId,
                    OrgId = instanceEvent.User.OrgId,
                    AuthenticationLevel = instanceEvent.User.AuthenticationLevel,
                    EndUserSystemId = instanceEvent.User.EndUserSystemId,
                    NationalIdentityNumber = instanceEvent.User.NationalIdentityNumber,
                    SystemUserId = instanceEvent.User.SystemUserId,
                    SystemUserOwnerOrgNo = instanceEvent.User.SystemUserOwnerOrgNo,
                    SystemUserName = instanceEvent.User.SystemUserName,
                }
                : null,
            RelatedUser = instanceEvent.RelatedUser != null
                ? new PlatformUser
                {
                    UserId = instanceEvent.RelatedUser.UserId,
                    OrgId = instanceEvent.RelatedUser.OrgId,
                    AuthenticationLevel = instanceEvent.RelatedUser.AuthenticationLevel,
                    EndUserSystemId = instanceEvent.RelatedUser.EndUserSystemId,
                    NationalIdentityNumber = instanceEvent.RelatedUser.NationalIdentityNumber,
                    SystemUserId = instanceEvent.RelatedUser.SystemUserId,
                    SystemUserOwnerOrgNo = instanceEvent.RelatedUser.SystemUserOwnerOrgNo,
                    SystemUserName = instanceEvent.RelatedUser.SystemUserName,
                }
                : null,
            ProcessInfo = instanceEvent.ProcessInfo != null
                ? new ProcessState
                {
                    Started = instanceEvent.ProcessInfo.Started,
                    StartEvent = instanceEvent.ProcessInfo.StartEvent,
                    CurrentTask = instanceEvent.ProcessInfo.CurrentTask,
                    Ended = instanceEvent.ProcessInfo.Ended,
                    EndEvent = instanceEvent.ProcessInfo.EndEvent
                }
                : null,
            AdditionalInfo = instanceEvent.AdditionalInfo
        };
    }
}
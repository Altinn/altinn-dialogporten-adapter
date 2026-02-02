using Altinn.Platform.Storage.Interface.Enums;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnInstanceEventBuilder
{
    private readonly InstanceEvent _instanceEvent;

    private AltinnInstanceEventBuilder(InstanceEvent instanceEvent)
    {
        _instanceEvent = instanceEvent;
    }

    public static AltinnInstanceEventBuilder From(InstanceEvent instanceEvent)
    {
        return new AltinnInstanceEventBuilder(instanceEvent.DeepClone());
    }

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
        _instanceEvent.Id = id;
        return this;
    }

    public AltinnInstanceEventBuilder WithInstanceId(string instanceId)
    {
        _instanceEvent.InstanceId = instanceId;
        return this;
    }

    public AltinnInstanceEventBuilder WithDataId(string dataId)
    {
        _instanceEvent.DataId = dataId;
        return this;
    }

    public AltinnInstanceEventBuilder WithCreated(DateTime created)
    {
        _instanceEvent.Created = created;
        return this;
    }

    public AltinnInstanceEventBuilder WithEventType(string eventType)
    {
        _instanceEvent.EventType = eventType;
        return this;
    }

    public AltinnInstanceEventBuilder WithInstanceOwnerPartyId(string partyId)
    {
        _instanceEvent.InstanceOwnerPartyId = partyId;
        return this;
    }

    public AltinnInstanceEventBuilder WithUser(PlatformUser user)
    {
        _instanceEvent.User = user;
        return this;
    }

    public AltinnInstanceEventBuilder WithRelatedUser(PlatformUser relatedUser)
    {
        _instanceEvent.RelatedUser = relatedUser;
        return this;
    }

    public AltinnInstanceEventBuilder WithProcessInfo(ProcessState processInfo)
    {
        _instanceEvent.ProcessInfo = processInfo;
        return this;
    }

    public AltinnInstanceEventBuilder WithAdditionalInfo(string info)
    {
        _instanceEvent.AdditionalInfo = info;
        return this;
    }

    public InstanceEvent Build()
    {
        return _instanceEvent.DeepClone();
    }
}
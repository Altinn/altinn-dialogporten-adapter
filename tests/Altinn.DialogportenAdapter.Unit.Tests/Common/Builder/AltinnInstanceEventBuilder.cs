using Altinn.Platform.Storage.Interface.Enums;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnInstanceEventBuilder
{
    private Guid? _id = Guid.NewGuid();
    private string _instanceId = "12345/67890";
    private string _dataId;
    private DateTime? _created = DateTime.UtcNow;
    private string _eventType = "created";
    private string _instanceOwnerPartyId = "party-1";
    private PlatformUser _user;
    private PlatformUser _relatedUser;
    private ProcessState _processInfo;
    private string _additionalInfo;

    private AltinnInstanceEventBuilder() {}

    public static AltinnInstanceEventBuilder NewCreatedByPlatformUserInstanceEvent(int userId) => new AltinnInstanceEventBuilder
    {
        _id = Guid.Parse("7488c4f3-1012-4487-b575-d38b01688da0"),
        _instanceId = "instance-id",
        _dataId = "data-id",
        _created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
        _eventType = nameof(InstanceEventType.Created),
        _instanceOwnerPartyId = "instance-owner-party-id",
        _user = new PlatformUser
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
        _relatedUser = null,
        _processInfo = null,
        _additionalInfo = null
    };

    public static AltinnInstanceEventBuilder NewSubmittedByPlatformUserInstanceEvent(int userId) => new AltinnInstanceEventBuilder
    {
        _id = Guid.Parse("019bfa70-c9f7-7f4d-bed1-d5417461bb53"),
        _instanceId = "instance-id",
        _dataId = "data-id",
        _created = new DateTime(2001, 1, 1, 1, 1, 1, DateTimeKind.Utc),
        _eventType = nameof(InstanceEventType.Submited),
        _instanceOwnerPartyId = "instance-owner-party-id",
        _user = new PlatformUser
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
        _relatedUser = null,
        _processInfo = null,
        _additionalInfo = null
    };

    public AltinnInstanceEventBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public AltinnInstanceEventBuilder WithInstanceId(string instanceId)
    {
        _instanceId = instanceId;
        return this;
    }

    public AltinnInstanceEventBuilder WithDataId(string dataId)
    {
        _dataId = dataId;
        return this;
    }

    public AltinnInstanceEventBuilder WithCreated(DateTime created)
    {
        _created = created;
        return this;
    }

    public AltinnInstanceEventBuilder WithEventType(string eventType)
    {
        _eventType = eventType;
        return this;
    }

    public AltinnInstanceEventBuilder WithInstanceOwnerPartyId(string partyId)
    {
        _instanceOwnerPartyId = partyId;
        return this;
    }

    public AltinnInstanceEventBuilder WithUser(PlatformUser user)
    {
        _user = user;
        return this;
    }

    public AltinnInstanceEventBuilder WithRelatedUser(PlatformUser relatedUser)
    {
        _relatedUser = relatedUser;
        return this;
    }

    public AltinnInstanceEventBuilder WithProcessInfo(ProcessState processInfo)
    {
        _processInfo = processInfo;
        return this;
    }

    public AltinnInstanceEventBuilder WithAdditionalInfo(string info)
    {
        _additionalInfo = info;
        return this;
    }

    public InstanceEvent Build()
    {
        return new InstanceEvent
        {
            Id = _id,
            InstanceId = _instanceId,
            DataId = _dataId,
            Created = _created,
            EventType = _eventType,
            InstanceOwnerPartyId = _instanceOwnerPartyId,
            User = _user,
            RelatedUser = _relatedUser,
            ProcessInfo = _processInfo,
            AdditionalInfo = _additionalInfo
        };
    }
}
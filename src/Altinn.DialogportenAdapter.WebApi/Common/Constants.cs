using System.Collections.Immutable;
using Altinn.Platform.Storage.Interface.Enums;

namespace Altinn.DialogportenAdapter.WebApi.Common;

public static class Constants
{
    public const int DefaultMaxStringLength = 255;

    public static readonly ImmutableArray<string> SupportedEventTypes =
    [
        InstanceEventType.Created.ToString(),
        InstanceEventType.Deleted.ToString(),
        InstanceEventType.Saved.ToString(),
        InstanceEventType.Submited.ToString(),
        InstanceEventType.Undeleted.ToString(),
        InstanceEventType.SubstatusUpdated.ToString(),
        InstanceEventType.Signed.ToString(),
        InstanceEventType.SentToSign.ToString(),
        InstanceEventType.SentToPayment.ToString(),
        InstanceEventType.SentToSendIn.ToString(),
        InstanceEventType.SentToFormFill.ToString(),
        InstanceEventType.InstanceForwarded.ToString(),
        InstanceEventType.InstanceRightRevoked.ToString(),
        InstanceEventType.NotificationSentSms.ToString(),
        InstanceEventType.MessageArchived.ToString(),
        InstanceEventType.MessageRead.ToString(),
    ];
}
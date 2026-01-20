using System.Collections.Immutable;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.Platform.Storage.Interface.Enums;

namespace Altinn.DialogportenAdapter.WebApi.Common;

internal static class Constants
{
    public const int DefaultMaxStringLength = 255;

    public const string InstanceDataValueDialogIdKey = "dialog.id";
    public const string InstanceDataValueDisableSyncKey = "dialog.disableAutomaticSync";

    public const string PartyIdUrnPrefix = "urn:altinn:party:id:";
    public const string UserIdUrnPrefix = "urn:altinn:user:id:";
    public const string PersonUrnPrefix = "urn:altinn:person:identifier-no:";
    public const string OrganizationUrnPrefix = "urn:altinn:organization:identifier-no:";
    public const string DisplayNameUrnPrefix = "urn:altinn:displayName:";
    public const string SiUserUrnPrefix = "urn:altinn:person:legacy-selfidentified:";

    public const string DefaultMaskinportenClientDefinitionKey = "DefaultMaskinportenClientDefinitionKey";

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

    public static readonly ImmutableArray<(DialogGuiActionPriority Priority, int Limit)> PriorityLimits = [
        (DialogGuiActionPriority.Primary, 1),
        (DialogGuiActionPriority.Secondary, 1),
        (DialogGuiActionPriority.Tertiary, 5 )
    ];

    internal static class GuiAction
    {
        public const string GoTo = "DialogGuiActionGoTo";
        public const string Delete = "DialogGuiActionDelete";
        public const string Copy = "DialogGuiActionCopy";

        public static readonly List<string> Keys = [ GoTo, Delete, Copy ];
    }
}
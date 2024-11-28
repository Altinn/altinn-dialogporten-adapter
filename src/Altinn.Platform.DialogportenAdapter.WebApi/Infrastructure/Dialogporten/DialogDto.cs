namespace Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;

public class DialogDto
{
    /// <summary>
    /// A self-defined UUIDv7 may be provided to support idempotent creation of dialogs. If not provided, a new UUIDv7 will be generated.
    /// </summary>
    /// <example>01913cd5-784f-7d3b-abef-4c77b1f0972d</example>
    public Guid? Id { get; set; }

    /// <summary>
    /// The service identifier for the service that the dialog is related to in URN-format.
    /// This corresponds to a resource in the Altinn Resource Registry, which the authenticated organization
    /// must own, i.e., be listed as the "competent authority" in the Resource Registry entry.
    /// </summary>
    /// <example>urn:altinn:resource:some-service-identifier</example>
    public string ServiceResource { get; set; } = null!;

    /// <summary>
    /// The party code representing the organization or person that the dialog belongs to in URN format.
    /// </summary>
    /// <example>
    /// urn:altinn:person:identifier-no:01125512345
    /// urn:altinn:organization:identifier-no:912345678
    /// </example>
    public string Party { get; set; } = null!;

    /// <summary>
    /// Advisory indicator of progress, represented as 1-100 percentage value. 100% representing a dialog that has come
    /// to a natural completion (successful or not).
    /// </summary>
    public int? Progress { get; set; }

    /// <summary>
    /// Arbitrary string with a service-specific indicator of status, typically used to indicate a fine-grained state of
    /// the dialog to further specify the "status" enum.
    /// </summary>
    public string? ExtendedStatus { get; set; }

    /// <summary>
    /// Arbitrary string with a service-specific reference to an external system or service.
    /// </summary>
    public string? ExternalReference { get; set; }

    /// <summary>
    /// The timestamp when the dialog should be made visible for authorized end users. If not provided, the dialog will be
    /// immediately available.
    /// </summary>
    /// <example>2022-12-31T23:59:59Z</example>
    public DateTimeOffset? VisibleFrom { get; set; }

    /// <summary>
    /// The due date for the dialog. Dialogs past due date might be marked as such in frontends but will still be available.
    /// </summary>
    /// <example>2022-12-31T23:59:59Z</example>
    public DateTimeOffset? DueAt { get; set; }

    /// <summary>
    /// Optional process identifier used to indicate a business process this dialog belongs to.
    /// </summary>
    public string? Process { get; set; }
    /// <summary>
    /// Optional preceding process identifier to indicate the business process that preceded the process indicated in the "Process" field. Cannot be set without also "Process" being set.
    /// </summary>
    public string? PrecedingProcess { get; set; }

    /// <summary>
    /// The expiration date for the dialog. This is the last date when the dialog is available for the end user.
    ///
    /// After this date is passed, the dialog will be considered expired and no longer available for the end user in any
    /// API. If not supplied, the dialog will be considered to never expire. This field can be changed after creation.
    /// </summary>
    /// <example>2022-12-31T23:59:59Z</example>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// If set, will override the date and time when the dialog is set as created.
    /// If not supplied, the current date /time will be used.
    /// </summary>
    /// <example>2022-12-31T23:59:59Z</example>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// If set, will override the date and time when the dialog is set as last updated.
    /// If not supplied, the current date /time will be used.
    /// </summary>
    /// <example>2022-12-31T23:59:59Z</example>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// The aggregated status of the dialog.
    /// </summary>
    public DialogStatus Status { get; set; }

    /// <summary>
    /// Set the system label of the dialog Migration purposes.
    /// </summary>
    public SystemLabel? SystemLabel { get; set; }
    /// <summary>
    /// The dialog unstructured text content.
    /// </summary>
    public ContentDto Content { get; set; } = null!;

    /// <summary>
    /// A list of words (tags) that will be used in dialog search queries. Not visible in end-user DTO.
    /// </summary>
    public List<SearchTagDto> SearchTags { get; set; } = [];

    /// <summary>
    /// The attachments associated with the dialog (on an aggregate level).
    /// </summary>
    public List<AttachmentDto> Attachments { get; set; } = [];

    /// <summary>
    /// The immutable list of transmissions associated with the dialog.
    /// </summary>
    public List<TransmissionDto> Transmissions { get; set; } = [];

    /// <summary>
    /// The GUI actions associated with the dialog. Should be used in browser-based interactive frontends.
    /// </summary>
    public List<GuiActionDto> GuiActions { get; set; } = [];

    /// <summary>
    /// The API actions associated with the dialog. Should be used in specialized, non-browser-based integrations.
    /// </summary>
    public List<ApiActionDto> ApiActions { get; set; } = [];

    /// <summary>
    /// An immutable list of activities associated with the dialog.
    /// </summary>
    public List<ActivityDto> Activities { get; set; } = [];
}

public enum SystemLabel
{
    Default = 1,
    Bin = 2,
    Archive = 3
}

public enum DialogStatus
{
    /// <summary>
    /// The dialogue is considered new. Typically used for simple messages that do not require any interaction,
    /// or as an initial step for dialogues. This is the default.
    /// </summary>
    New = 1,

    /// <summary>
    /// Started. In a serial process, this is used to indicate that, for example, a form filling is ongoing.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// Used to indicate user-initiated dialogs not yet sent.
    /// </summary>
    Draft = 3,

    /// <summary>
    /// Sent by the service owner. In a serial process, this is used after a submission is made.
    /// </summary>
    Sent = 4,

    /// <summary>
    /// Used to indicate that the dialogue is in progress/under work, but is in a state where the user must do something - for example, correct an error, or other conditions that hinder further processing.
    /// </summary>
    RequiresAttention = 5,

    /// <summary>
    /// The dialogue was completed. This typically means that the dialogue is moved to a GUI archive or similar.
    /// </summary>
    Completed = 6
}

public sealed class TransmissionDto
{
    /// <summary>
    /// A self-defined UUIDv7 may be provided to support idempotent creation of transmissions. If not provided, a new UUIDv7 will be generated.
    /// </summary>
    /// <example>01913cd5-784f-7d3b-abef-4c77b1f0972d</example>
    public Guid? Id { get; set; }

    /// <summary>
    /// If supplied, overrides the creating date and time for the transmission.
    /// If not supplied, the current date /time will be used.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Contains an authorization resource attributeId, that can used in custom authorization rules in the XACML service
    /// policy, which by default is the policy belonging to the service referred to by "serviceResource" in the dialog.
    ///
    /// Can also be used to refer to other service policies.
    /// </summary>
    /// <example>
    /// mycustomresource
    /// /* equivalent to the above */
    /// urn:altinn:subresource:mycustomresource
    /// urn:altinn:task:Task_1
    /// /* refer to another service */
    /// urn:altinn:resource:some-other-service-identifier
    /// </example>
    public string? AuthorizationAttribute { get; set; }

    /// <summary>
    /// Arbitrary URI/URN describing a service-specific transmission type.
    ///
    /// Refer to the service-specific documentation provided by the service owner for details (if in use).
    /// </summary>
    public Uri? ExtendedType { get; set; }

    /// <summary>
    /// Reference to any other transmission that this transmission is related to.
    /// </summary>
    public Guid? RelatedTransmissionId { get; set; }

    /// <summary>
    /// The type of transmission.
    /// </summary>
    public DialogTransmissionType Type { get; set; }

    /// <summary>
    /// The actor that sent the transmission.
    /// </summary>
    public ActorDto Sender { get; set; } = null!;

    /// <summary>
    /// The transmission unstructured text content.
    /// </summary>
    public TransmissionContentDto Content { get; set; } = null!;

    /// <summary>
    /// The transmission-level attachments.
    /// </summary>
    public List<TransmissionAttachmentDto> Attachments { get; set; } = [];
}

public enum DialogTransmissionType
{
    /// <summary>
    /// For general information, not related to any submissions
    /// </summary>
    Information = 1,

    /// <summary>
    /// Feedback/receipt accepting a previous submission
    /// </summary>
    Acceptance = 2,

    /// <summary>
    /// Feedback/error message rejecting a previous submission
    /// </summary>
    Rejection = 3,

    /// <summary>
    /// Question/request for more information
    /// </summary>
    Request = 4,

    /// <summary>
    /// Critical information about the process
    /// </summary>
    Alert = 5,

    /// <summary>
    /// Information about a formal decision ("resolution")
    /// </summary>
    Decision = 6,

    /// <summary>
    /// A normal submission of some information/form
    /// </summary>
    Submission = 7,

    /// <summary>
    /// A submission correcting/overriding some previously submitted information
    /// </summary>
    Correction = 8
}

public sealed class ContentDto
{
    /// <summary>
    /// The title of the dialog.
    /// Supported media types: text/plain
    /// </summary>
    public ContentValueDto Title { get; set; } = null!;

    /// <summary>
    /// A short summary of the dialog and its current state.
    /// Supported media types: text/plain
    /// </summary>
    public ContentValueDto Summary { get; set; } = null!;

    /// <summary>
    /// Overridden sender name. If not supplied, assume "org" as the sender name. Must be text/plain if supplied.
    /// Supported media types: text/plain
    /// </summary>
    public ContentValueDto? SenderName { get; set; }

    /// <summary>
    /// Additional information about the dialog.
    /// Supported media types: text/plain, text/markdown
    /// </summary>
    public ContentValueDto? AdditionalInfo { get; set; }

    /// <summary>
    /// Used as the human-readable label used to describe the "ExtendedStatus" field.
    /// Supported media types: text/plain
    /// </summary>
    public ContentValueDto? ExtendedStatus { get; set; }

    /// <summary>
    /// Front-channel embedded content. Used to dynamically embed content in the frontend from an external URL. Must be HTTPS.
    /// Supported media types: application/vnd.dialogporten.frontchannelembed+json;type=markdown
    /// </summary>
    public ContentValueDto? MainContentReference { get; set; }
}

public sealed class TransmissionContentDto
{
    /// <summary>
    /// The transmission title. Must be text/plain.
    /// </summary>
    public ContentValueDto Title { get; set; } = null!;

    /// <summary>
    /// The transmission summary.
    /// </summary>
    public ContentValueDto Summary { get; set; } = null!;

    /// <summary>
    /// Front-channel embedded content. Used to dynamically embed content in the frontend from an external URL. Must be HTTPS.
    /// Allowed media types: application/vnd.dialogporten.frontchannelembed+json;type=markdown
    /// </summary>
    public ContentValueDto? ContentReference { get; set; }
}

public sealed class SearchTagDto
{
    /// <summary>
    /// A search tag value.
    /// </summary>
    public string Value { get; set; } = null!;
}

public sealed class ActivityDto
{
    /// <summary>
    /// A self-defined UUIDv7 may be provided to support idempotent creation of activities. If not provided, a new UUIDv7 will be generated.
    /// </summary>
    /// <example>01913cd5-784f-7d3b-abef-4c77b1f0972d</example>
    public Guid? Id { get; set; }

    /// <summary>
    /// If supplied, overrides the creating date and time for the transmission.
    /// If not supplied, the current date /time will be used.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>
    /// Arbitrary URI/URN describing a service-specific transmission type.
    /// </summary>
    public Uri? ExtendedType { get; set; }

    /// <summary>
    /// The type of transmission.
    /// </summary>
    public DialogActivityType Type { get; set; }

    /// <summary>
    /// If the activity is related to a particular transmission, this field will contain the transmission identifier.
    /// Must be present in the request body.
    /// </summary>
    public Guid? TransmissionId { get; set; }

    /// <summary>
    /// The actor that performed the activity.
    /// </summary>
    public ActorDto PerformedBy { get; set; } = null!;

    /// <summary>
    /// Unstructured text describing the activity. Only set if the activity type is "Information".
    /// </summary>
    public List<LocalizationDto> Description { get; set; } = [];
}

public enum DialogActivityType
{
    /// <summary>
    /// Refers to a dialog that has been created.
    /// </summary>
    DialogCreated = 1,

    /// <summary>
    /// Refers to a dialog that has been closed.
    /// </summary>
    DialogClosed = 2,

    /// <summary>
    /// Information from the service provider, not (directly) related to any transmission.
    /// </summary>
    Information = 3,

    /// <summary>
    /// Refers to a transmission that has been opened.
    /// </summary>
    TransmissionOpened = 4,

    /// <summary>
    /// Indicates that payment has been made.
    /// </summary>
    PaymentMade = 5,

    /// <summary>
    /// Indicates that a signature has been provided.
    /// </summary>
    SignatureProvided = 6,

    /// <summary>
    /// Refers to a dialog that has been opened.
    /// </summary>
    DialogOpened = 7,
}

public sealed class ApiActionDto
{
    /// <summary>
    /// String identifier for the action, corresponding to the "action" attributeId used in the XACML service policy,
    /// which by default is the policy belonging to the service referred to by "serviceResource" in the dialog.
    /// </summary>
    /// <example>write</example>
    public string Action { get; set; } = null!;

    /// <summary>
    /// Contains an authorization resource attributeId, that can used in custom authorization rules in the XACML service
    /// policy, which by default is the policy belonging to the service referred to by "serviceResource" in the dialog.
    ///
    /// Can also be used to refer to other service policies.
    /// </summary>
    /// <example>
    /// mycustomresource
    /// /* equivalent to the above */
    /// urn:altinn:subresource:mycustomresource
    /// urn:altinn:task:Task_1
    /// /* refer to another service */
    /// urn:altinn:resource:some-other-service-identifier
    /// </example>
    public string? AuthorizationAttribute { get; set; }

    /// <summary>
    /// The endpoints associated with the action.
    /// </summary>
    public List<ApiActionEndpointDto> Endpoints { get; set; } = [];
}

public sealed class ApiActionEndpointDto
{
    /// <summary>
    /// Arbitrary string indicating the version of the endpoint.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// The fully qualified URL of the API endpoint.
    /// </summary>
    public Uri Url { get; set; } = null!;

    /// <summary>
    /// The HTTP method that the endpoint expects for this action.
    /// </summary>
    public HttpVerb HttpMethod { get; set; }

    /// <summary>
    /// Link to documentation for the endpoint, providing documentation for integrators. Should be a URL to a
    /// human-readable page.
    /// </summary>
    public Uri? DocumentationUrl { get; set; }

    /// <summary>
    /// Link to the request schema for the endpoint. Used to provide documentation for integrators.
    /// Dialogporten will not validate information on this endpoint.
    /// </summary>
    public Uri? RequestSchema { get; set; }

    /// <summary>
    /// Link to the response schema for the endpoint. Used to provide documentation for integrators.
    /// Dialogporten will not validate information on this endpoint.
    /// </summary>
    public Uri? ResponseSchema { get; set; }

    /// <summary>
    /// Boolean indicating if the endpoint is deprecated.
    /// </summary>
    public bool Deprecated { get; set; }

    /// <summary>
    /// Date and time when the endpoint will no longer function. Only set if the endpoint is deprecated. Dialogporten
    /// will not enforce this date.
    /// </summary>
    public DateTimeOffset? SunsetAt { get; set; }
}

public enum HttpVerb
{
    GET = 1,
    POST = 2,
    PUT = 3,
    PATCH = 4,
    DELETE = 5,
    HEAD = 6,
    OPTIONS = 7,
    TRACE = 8,
    CONNECT = 9
}

public sealed class GuiActionDto
{
    /// <summary>
    /// The action identifier for the action, corresponding to the "action" attributeId used in the XACML service policy.
    /// </summary>
    public string Action { get; set; } = null!;

    /// <summary>
    /// The fully qualified URL of the action, to which the user will be redirected when the action is triggered. Will be set to
    /// "urn:dialogporten:unauthorized" if the user is not authorized to perform the action.
    /// </summary>
    /// <example>
    /// urn:dialogporten:unauthorized
    /// https://someendpoint.com/gui/some-service-instance-id
    /// </example>
    public Uri Url { get; set; } = null!;

    /// <summary>
    /// Contains an authorization resource attributeId, that can used in custom authorization rules in the XACML service
    /// policy, which by default is the policy belonging to the service referred to by "serviceResource" in the dialog.
    ///
    /// Can also be used to refer to other service policies.
    /// </summary>
    /// <example>
    /// mycustomresource
    /// /* equivalent to the above */
    /// urn:altinn:subresource:mycustomresource
    /// urn:altinn:task:Task_1
    /// /* refer to another service */
    /// urn:altinn:resource:some-other-service-identifier
    /// </example>
    public string? AuthorizationAttribute { get; set; }

    /// <summary>
    /// Indicates whether the action results in the dialog being deleted. Used by frontends to implement custom UX
    /// for delete actions.
    /// </summary>
    public bool IsDeleteDialogAction { get; set; }

    /// <summary>
    /// The HTTP method that the frontend should use when redirecting the user.
    /// </summary>
    public HttpVerb? HttpMethod { get; set; } = HttpVerb.GET;

    /// <summary>
    /// Indicates a priority for the action, making it possible for frontends to adapt GUI elements based on action
    /// priority.
    /// </summary>
    public DialogGuiActionPriority Priority { get; set; }

    /// <summary>
    /// The title of the action, this should be short and in verb form. Must be text/plain.
    /// </summary>
    public List<LocalizationDto> Title { get; set; } = [];

    /// <summary>
    /// If there should be a prompt asking the user for confirmation before the action is executed,
    /// this field should contain the prompt text.
    /// </summary>
    public List<LocalizationDto>? Prompt { get; set; }
}

public enum DialogGuiActionPriority
{
    Primary = 1,
    Secondary = 2,
    Tertiary = 3
}

public sealed class AttachmentDto
{
    /// <summary>
    /// The display name of the attachment that should be used in GUIs.
    /// </summary>
    public List<LocalizationDto> DisplayName { get; set; } = [];

    /// <summary>
    /// The URLs associated with the attachment, each referring to a different representation of the attachment.
    /// </summary>
    public List<AttachmentUrlDto> Urls { get; set; } = [];
}

public sealed class AttachmentUrlDto
{
    /// <summary>
    /// The fully qualified URL of the attachment.
    /// </summary>
    public Uri Url { get; set; } = null!;

    /// <summary>
    /// The media type of the attachment.
    /// </summary>
    /// <example>
    /// application/pdf
    /// application/zip
    /// </example>
    public string? MediaType { get; set; } = null!;

    /// <summary>
    /// The type of consumer the URL is intended for.
    /// </summary>
    public AttachmentUrlConsumerType ConsumerType { get; set; }
}

public enum AttachmentUrlConsumerType
{
    Gui = 1,
    Api = 2
}

public sealed class TransmissionAttachmentDto
{
    /// <summary>
    /// A self-defined UUIDv7 may be provided to support idempotent creation of transmission attachments. If not provided, a new UUIDv7 will be generated.
    /// </summary>
    /// <example>01913cd5-784f-7d3b-abef-4c77b1f0972d</example>
    public Guid? Id { get; set; }

    /// <summary>
    /// The display name of the attachment that should be used in GUIs.
    /// </summary>
    public List<LocalizationDto> DisplayName { get; set; } = [];

    /// <summary>
    /// The URLs associated with the attachment, each referring to a different representation of the attachment.
    /// </summary>
    public List<TransmissionAttachmentUrlDto> Urls { get; set; } = [];
}

public sealed class TransmissionAttachmentUrlDto
{
    /// <summary>
    /// The fully qualified URL of the attachment.
    /// </summary>
    public Uri Url { get; set; } = null!;

    /// <summary>
    /// The media type of the attachment.
    /// </summary>
    /// <example>
    /// application/pdf
    /// application/zip
    /// </example>
    public string? MediaType { get; set; } = null!;

    /// <summary>
    /// The type of consumer the URL is intended for.
    /// </summary>
    public AttachmentUrlConsumerType ConsumerType { get; set; }
}

public sealed class LocalizationDto
{
    private readonly string _languageCode = null!;

    /// <summary>
    /// The localized text or URI reference.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// The language code of the localization in ISO 639-1 format.
    /// </summary>
    /// <example>nb</example>
    public required string LanguageCode { get; init; }
}

public sealed class ActorDto
{
    /// <summary>
    /// The type of actor that sent the transmission.
    /// </summary>
    public ActorType ActorType { get; set; }

    /// <summary>
    /// Specifies the name of the entity that sent the transmission. Mutually exclusive with ActorId. If ActorId
    /// is supplied, the name will be automatically populated from the name registries.
    /// </summary>
    /// <example>Ola Nordmann</example>
    public string? ActorName { get; set; }

    /// <summary>
    /// The identifier of the person or organization that sent the transmission. Mutually exclusive with ActorName.
    /// Might be omitted if ActorType is "ServiceOwner".
    /// </summary>
    /// <example>urn:altinn:person:identifier-no:12018212345</example>
    public string? ActorId { get; set; }
}

public enum ActorType
{
    PartyRepresentative = 1,
    ServiceOwner = 2
}

public sealed class ContentValueDto
{
    /// <summary>
    /// A list of localizations for the content.
    /// </summary>
    public List<LocalizationDto> Value { get; set; } = [];

    /// <summary>
    /// Media type of the content (plaintext, Markdown). Can also indicate that the content is embeddable.
    /// </summary>
    public string MediaType { get; set; } = "text/plain";
}
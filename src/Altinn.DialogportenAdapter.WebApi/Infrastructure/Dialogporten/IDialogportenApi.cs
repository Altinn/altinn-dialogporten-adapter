using System.Text.Json.Serialization;
using Refit;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;

internal interface IDialogportenApi
{
    public const string IfMatchHeader = "If-Match";
    public const string ETagHeader = "ETag";

    [Get("/api/v1/serviceowner/dialogs/{dialogId}")]
    Task<IApiResponse<DialogDto>> Get(Guid dialogId, CancellationToken cancellationToken = default);

    [Post("/api/v1/serviceowner/dialogs")]
    Task<IApiResponse> Create([Body] DialogDto dto, [Query] bool isSilentUpdate = false, CancellationToken cancellationToken = default);

    [Put("/api/v1/serviceowner/dialogs/{dto.Id}")]
    Task<IApiResponse> Update([Body] DialogDto dto, [Header(IfMatchHeader)] Guid revision, [Query] bool isSilentUpdate = false, CancellationToken cancellationToken = default);

    [Delete("/api/v1/serviceowner/dialogs/{dialogId}")]
    Task<IApiResponse> Delete(Guid dialogId, [Header(IfMatchHeader)] Guid revision, [Query] bool isSilentUpdate = false, CancellationToken cancellationToken = default);

    [Post("/api/v1/serviceowner/dialogs/{dialogId}/actions/purge")]
    Task<IApiResponse> Purge(Guid dialogId, [Header(IfMatchHeader)] Guid revision, [Query] bool isSilentUpdate = false, CancellationToken cancellationToken = default);

    [Post("/api/v1/serviceowner/dialogs/{dialogId}/actions/restore")]
    Task<IApiResponse> Restore(Guid dialogId, [Header(IfMatchHeader)] Guid revision, [Query] bool isSilentUpdate = false, CancellationToken cancellationToken = default);

    [Get("/api/v1/serviceowner/dialogs")]
    Task<IApiResponse<PaginatedListOfDialogs>> SearchByServiceOwnerLabels([Query(CollectionFormat.Multi)] IEnumerable<string> serviceOwnerLabels, CancellationToken cancellationToken = default);


    [Post("/api/v1/serviceowner/dialogs/{dialogId}/activities/{activityId}/actions/updateFormSavedActivityTime")]
    Task<IApiResponse> UpdateFormSavedActivityTime(
        Guid dialogId,
        Guid activityId,
        [Header(IfMatchHeader)] Guid revision,
        [Body] DateTimeOffset newCreatedAt,
        CancellationToken cancellationToken = default);
}

public class PaginatedListOfDialogs
{

    /// <summary>
    /// The paginated list of items
    /// </summary>
    [JsonPropertyName("items")]
    public ICollection<DialogDto> Items { get; set; } = null!;

    /// <summary>
    /// Whether there are more items available that can be fetched by supplying the continuation token
    /// </summary>
    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage { get; set; }

    /// <summary>
    /// The continuation token to be used to fetch the next page of items
    /// </summary>
    [JsonPropertyName("continuationToken")]
    public string ContinuationToken { get; set; } = null!;

    /// <summary>
    /// The current sorting order of the items
    /// </summary>
    [JsonPropertyName("orderBy")]
    public string OrderBy { get; set; } = null!;

}

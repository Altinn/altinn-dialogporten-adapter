
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
    Task Delete(Guid dialogId, [Header(IfMatchHeader)] Guid revision, [Query] bool isSilentUpdate = false, CancellationToken cancellationToken = default);

    [Post("/api/v1/serviceowner/dialogs/{dialogId}/actions/purge")]
    Task Purge(Guid dialogId, [Header(IfMatchHeader)] Guid revision, [Query] bool isSilentUpdate = false, CancellationToken cancellationToken = default);

    [Post("/api/v1/serviceowner/dialogs/{dialogId}/actions/restore")]
    Task<IApiResponse> Restore(Guid dialogId, [Header(IfMatchHeader)] Guid revision, [Query] bool isSilentUpdate = false, CancellationToken cancellationToken = default);

    [Post("/api/v1/serviceowner/dialogs/{dialogId}/activities/{activityId}/actions/updateFormSavedActivityTime")]
    Task<IApiResponse> UpdateFormSavedActivityTime(
        Guid dialogId,
        Guid activityId,
        [Header(IfMatchHeader)] Guid revision,
        [Body] DateTimeOffset newCreatedAt,
        CancellationToken cancellationToken = default);
}

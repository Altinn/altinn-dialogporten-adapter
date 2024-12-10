using Refit;

namespace Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;

internal interface IDialogportenApi
{
    [Get("/api/v1/serviceowner/dialogs/{dialogId}")]
    Task<IApiResponse<DialogDto>> Get(Guid dialogId, CancellationToken cancellationToken = default);

    [Post("/api/v1/serviceowner/dialogs")]
    Task<Guid> Create([Body] DialogDto dto, CancellationToken cancellationToken = default);
    
    [Put("/api/v1/serviceowner/dialogs/{dto.Id}")]
    Task Update([Body] DialogDto dto, [Header("If-Match")] Guid revision, CancellationToken cancellationToken = default);
    
    [Delete("/api/v1/serviceowner/dialogs/{dialogId}")]
    Task Delete(Guid dialogId, [Header("If-Match")] Guid revision, CancellationToken cancellationToken = default);
    
    [Delete("/api/v1/serviceowner/dialogs/{dialogId}/actions/purge")]
    Task Purge(Guid dialogId, [Header("If-Match")] Guid revision, CancellationToken cancellationToken = default);
}
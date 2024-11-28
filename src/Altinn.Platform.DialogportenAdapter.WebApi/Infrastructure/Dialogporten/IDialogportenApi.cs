using Refit;

namespace Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;

public interface IDialogportenApi
{
    [Post("/api/v1/serviceowner/dialogs")]
    Task<IApiResponse<Guid>> Create(DialogDto dto, CancellationToken cancellationToken = default);
    
    [Get("/api/v1/serviceowner/dialogs/{dialogId}")]
    Task<IApiResponse<DialogDto>> Get(Guid dialogId, CancellationToken cancellationToken = default);
}
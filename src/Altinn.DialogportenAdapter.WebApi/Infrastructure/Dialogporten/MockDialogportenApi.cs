using System.Net;
using Refit;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;

internal sealed class MockDialogportenApi : IDialogportenApi
{
    private static readonly RefitSettings RefitSettings = new();
    public Task<IApiResponse<DialogDto>> Get(Guid dialogId, CancellationToken cancellationToken = default)
    {
        var apiResponse = new ApiResponse<DialogDto>(
            response: new HttpResponseMessage(HttpStatusCode.NotFound),
            content: null,
            settings: RefitSettings,
            error: null);
        return Task.FromResult<IApiResponse<DialogDto>>(apiResponse);
    }

    public Task<Guid> Create(DialogDto dto, bool disableAltinnEvents = false, bool disableSystemLabelReset = false,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Guid.Empty);
    }

    public Task Update(DialogDto dto, Guid revision, bool disableAltinnEvents = false, bool disableSystemLabelReset = false,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task Delete(Guid dialogId, Guid revision, bool disableAltinnEvents = false,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task Purge(Guid dialogId, Guid revision, bool disableAltinnEvents = false,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IApiResponse> Restore(Guid dialogId, Guid revision, bool disableAltinnEvents = false,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
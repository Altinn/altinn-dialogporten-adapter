using Altinn.Platform.Storage.Interface.Models;
using Refit;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;

public interface IApplicationsApi
{
    [Get("/storage/api/v1/applications/{**appId}")]
    Task<IApiResponse<Application>> GetApplication(string appId, CancellationToken cancellationToken = default);
    
    [Get("/storage/api/v1/applications/{org}/{app}/texts/{language}")]
    Task<IApiResponse<TextResource>> GetApplicationTexts(string org, string app, string language, CancellationToken cancellationToken = default);
}

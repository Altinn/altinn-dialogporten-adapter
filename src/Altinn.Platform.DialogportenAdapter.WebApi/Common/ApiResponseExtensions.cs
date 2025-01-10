using System.Net;
using Refit;

namespace Altinn.Platform.DialogportenAdapter.WebApi.Common;

internal static class ApiResponseExtensions
{
    public static async Task<T?> ContentOrDefault<T>(this Task<IApiResponse<T>> responseTask)
    {
        var response = await responseTask;
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
        
        return response.IsSuccessful
            ? response.Content
            : throw response.Error;
    }
    
    public static async Task<T> EnsureSuccess<T>(this Task<T> responseTask)
        where T : IApiResponse
    {
        var response = await responseTask;
        return response.IsSuccessful 
            ? response 
            : throw response.Error;
    }
}
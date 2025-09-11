using System.Diagnostics;
using System.Net;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Refit;

namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

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

    public static Guid GetEtagHeader(this IApiResponse response) => 
        !response.TryGetEtagHeader(out var etag) 
            ? throw new UnreachableException("ETag header was not found or could not be parsed.") 
            : etag;
    
    public static bool TryGetEtagHeader(this IApiResponse response, out Guid etag)
    {
        etag = Guid.Empty;
        return response.Headers.TryGetValues(IDialogportenApi.ETagHeader, out var etags) &&
               Guid.TryParse(etags.FirstOrDefault(), out etag);
    }
}
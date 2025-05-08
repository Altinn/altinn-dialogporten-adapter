using System.Net;

namespace Altinn.DialogportenAdapter.WebApi.Common;

internal sealed class FourHundredLoggingDelegatingHandler : DelegatingHandler
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<FourHundredLoggingDelegatingHandler> _logger;

    public FourHundredLoggingDelegatingHandler(IHostEnvironment hostEnvironment, ILogger<FourHundredLoggingDelegatingHandler> logger)
    {
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return !_hostEnvironment.IsProduction() && _logger.IsEnabled(LogLevel.Information)
            ? SendAsync_Internal(request, cancellationToken)
            : base.SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync_Internal(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await (request.Content?.LoadIntoBufferAsync(cancellationToken) ?? Task.CompletedTask);
        var requestContent = new Lazy<Task<string>>(() =>
            request.Content?.ReadAsStringAsync(cancellationToken) ?? Task.FromResult(string.Empty));
        HttpResponseMessage? response;

        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (Refit.ApiException e)
        {
            if (ShouldLog(e.StatusCode))
            {
                _logger.LogRequestError(e, request.Method, request.RequestUri, await requestContent.Value, e.StatusCode, e.Content);
            }
            throw;
        }

        if (!ShouldLog(response.StatusCode))
        {
            return response;
        }

        await response.Content.LoadIntoBufferAsync(cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.Log400Response(request.Method, request.RequestUri, await requestContent.Value, response.StatusCode, responseContent);
        return response;
    }

    private static bool ShouldLog(HttpStatusCode statusCode) =>
        (int)statusCode is >= 400 and < 500 and not 404;
}


internal static partial class LogMessages
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "{Method} {RequestUri} resulted in {StatusCode}.\nRequest: {Request}\nResponse: {Response}")]
    public static partial void Log400Response(this ILogger logger, HttpMethod method, Uri? requestUri, string? request, HttpStatusCode statusCode, string? response);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "{Method} {RequestUri} resulted in {StatusCode}.\nRequest: {Request}\nResponse: {Response}")]
    public static partial void LogRequestError(this ILogger logger, Exception exception, HttpMethod method, Uri? requestUri, string? request, HttpStatusCode statusCode, string? response);
}
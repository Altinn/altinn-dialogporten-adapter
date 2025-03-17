using System.Net;

namespace Altinn.DialogportenAdapter.WebApi;

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
        return !_hostEnvironment.IsProduction() && _logger.IsEnabled(LogLevel.Debug)
            ? SendAsync_Internal(request, cancellationToken)
            : base.SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync_Internal(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await (request.Content?.LoadIntoBufferAsync(cancellationToken) ?? Task.CompletedTask);
        var response = await base.SendAsync(request, cancellationToken);
        if ((int)response.StatusCode is 404 || 
            (int)response.StatusCode is <= 400 or > 499)
        {
            return response;
        }

        await response.Content.LoadIntoBufferAsync(cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var requestContent = await (request.Content?.ReadAsStringAsync(cancellationToken) ?? Task.FromResult(string.Empty));
        
        _logger.Log400Response(request.Method, 
            request.RequestUri, 
            response.StatusCode,
            requestContent,
            responseContent);
        
        return response;
    }
}


internal static partial class LogMessages
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "{Method} {RequestUri} resulted in {StatusCode}. {Request}, {Response}")]
    public static partial void Log400Response(this ILogger logger, HttpMethod method, Uri? requestUri, HttpStatusCode statusCode, string request, string response);
}
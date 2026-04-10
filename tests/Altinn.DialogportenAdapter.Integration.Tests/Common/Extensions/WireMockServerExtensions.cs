using System.Net;
using Microsoft.Extensions.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Altinn.DialogportenAdapter.Integration.Tests.Common.Extensions;

public static partial class WireMockServerExtensions
{
    private static readonly Guid WireMockFallbackMappingGuid = Guid.NewGuid();

    extension(WireMockServer server)
    {
        /// <summary>
        /// Adds a fallback mapping that matches all requests, but with the lowest priority.
        /// The fallback response status code will be 501 Not Implemented.
        /// All events, that get 501 from any api-call, goes to the DLQ.
        /// </summary>
        /// <returns></returns>
        public WireMockServer AddCustomFallbackMapping()
        {
            server.Given(Request.Create().UsingAnyMethod().WithPath(_ => true))
                .WithGuid(WireMockFallbackMappingGuid)
                .AtPriority(short.MaxValue)
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.NotImplemented)
                    .WithBody("No endpoint was mapped yet"));

            return server;
        }

        /// <summary>
        /// Resets all log entries and deletes all mappings, except the fallback mapping.
        /// This makes sure events, that linger after a test, will fail fast and still go to the DLQ.
        /// The default response in wiremock is 404 and this is an expected response from some of our api-calls
        /// This means we can get unexpected results if we do a full wiremock reset.
        /// </summary>
        /// <returns></returns>
        public WireMockServer ResetAllExceptFallbackMapping()
        {
            server.ResetLogEntries();

            foreach (var mapping in server.Mappings.Where(m => m.Guid != WireMockFallbackMappingGuid))
            {
                server.DeleteMapping(mapping.Guid);
            }

            return server;
        }

        public WireMockServer LogUnhandledRequests(ILogger logger, string serverName)
        {
            server.LogEntries.Where(log =>
                {
                    var status = log.ResponseMessage?.StatusCode;
                    if (status == null) return true;
                    return (int)status == 501;
                })
                .Select(x => x.RequestMessage)
                .Where(x => x != null)
                .ToList()
                .ForEach(x => LogNoWiremockHandler(logger, serverName, x!.Method, x.Url));

            return server;
        }
    }

    [LoggerMessage(LogLevel.Warning, "No wiremock handler for {serverName} - {Method}: {Url}")]
    static partial void LogNoWiremockHandler(ILogger logger, string serverName, string method, string url);
}

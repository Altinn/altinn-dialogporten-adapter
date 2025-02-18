using Microsoft.Extensions.Logging.ApplicationInsights;

namespace Altinn.DialogportenAdapter.WebApi.Common;

/// <summary>
/// Extension method(s) helping with setting up logging for the application during startup
/// </summary>
public static class ApplicationLogging
{
    /// <summary>
    /// This method is responsible for setting up logging to console and Application Insights (AI) if a
    /// connection string to AI is provided.
    /// </summary>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> instance used by application builder.</param>
    /// <param name="applicationInsightsConnectionString">A connection string for an AI service.</param>
    /// <returns>Returns the original <see cref="ILoggingBuilder"/>.</returns>
    public static ILoggingBuilder ConfigureApplicationLogging(
        this ILoggingBuilder builder, 
        string? applicationInsightsConnectionString = null)
    {
        // The default ASP.NET Core project templates call CreateDefaultBuilder, which adds the following logging providers:
        // Console, Debug, EventSource
        // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-3.1

        // Clear log providers
        builder.ClearProviders();

        if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
        {
            // Add application insights https://docs.microsoft.com/en-us/azure/azure-monitor/app/ilogger
            builder.AddApplicationInsights(
                configureTelemetryConfiguration: (config) => config.ConnectionString = applicationInsightsConnectionString,
                configureApplicationInsightsLoggerOptions: (options) => { });

            // Optional: Apply filters to control what logs are sent to Application Insights.
            // The following configures LogLevel Information or above to be sent to
            // Application Insights for all categories.
            builder.AddFilter<ApplicationInsightsLoggerProvider>(string.Empty, LogLevel.Warning);

            // Adding the filter below to ensure logs of all severity from Program.cs
            // is sent to ApplicationInsights.
            builder.AddFilter<ApplicationInsightsLoggerProvider>(typeof(Program).FullName, LogLevel.Trace);
        }
        else
        {
            builder.AddFilter("Microsoft", LogLevel.Warning);
            builder.AddFilter("System", LogLevel.Warning);
        }

        builder.AddConsole();

        return builder;
    }
}
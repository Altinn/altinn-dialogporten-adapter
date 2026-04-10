using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.WebApi;
using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Delete;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using var loggerFactory = CreateBootstrapLoggerFactory();
var bootstrapLogger = loggerFactory.CreateLogger<Program>();

try
{
    BuildAndRun(args);
}
catch (Exception e)
{
    LogApplicationTerminatedUnexpectedly(bootstrapLogger, e);
    throw;
}

return;

static void BuildAndRun(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging
        .ClearProviders()
        .AddConsole();

    builder.Configuration
        .AddCoreClusterSettings()
        .AddAzureKeyVault()
        .AddLocalDevelopmentSettings(builder.Environment);

    builder.Services
        .ConfigureDialogportenAdapterServices(builder.Configuration, builder.Environment, new Clock())
        .ReplaceLocalDevelopmentResources(builder.Configuration, builder.Environment);

    using var app = builder.Build();

    app.UseHttpsRedirection()
        .UseCors()
        .UseAuthentication()
        .UseAuthorization();

    var baseRoute = app.MapGroup("storage/dialogporten");
    var v1Route = baseRoute.MapGroup("api/v1");
    var settings = app.Services.GetRequiredService<IOptions<Settings>>().Value;

    app.MapHealthChecks("/health")
        .AllowAnonymous();

    app.MapOpenApi()
        .AllowAnonymous();

    v1Route.MapPost("syncDialog", async (
        [FromBody] SyncInstanceCommand request,
        [FromServices] ISyncInstanceToDialogService syncService,
        CancellationToken cancellationToken) =>
    {
        await syncService.Sync(request, cancellationToken);
        return Results.NoContent();
    })
    .RequireAuthorization();

    v1Route.MapPost("syncDialog/simple/{partyId}/{instanceGuid:guid}", async (
            [FromRoute] string partyId,
            [FromRoute] Guid instanceGuid,
            [FromQuery] bool? isMigration,
            [FromServices] ISyncInstanceToDialogService syncService,
            [FromServices] IStorageApi storageApi,
            CancellationToken cancellationToken) =>
        {
            var instance = await storageApi
                .GetInstance(partyId, instanceGuid, cancellationToken)
                .ContentOrDefault();
            if (instance is null)
            {
                return Results.NotFound();
            }

            var request = new SyncInstanceCommand(instance.AppId, partyId, instanceGuid, instance.Created!.Value, isMigration ?? false);
            await syncService.Sync(request, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .ExcludeFromDescription();

    v1Route.MapDelete("instance/{instanceOwner}/{instanceGuid:guid}", async (
            [FromRoute] string instanceOwner,
            [FromRoute] Guid instanceGuid,
            [FromHeader(Name = "Authorization")] string authorization,
            [FromServices] InstanceService instanceService,
            CancellationToken cancellationToken) =>
        {
            var request = new DeleteInstanceDto(instanceOwner, instanceGuid, authorization);
            return await instanceService.Delete(request, cancellationToken) switch
            {
                DeleteResponse.Success => Results.NoContent(),
                DeleteResponse.UnAuthorized => Results.Unauthorized(),
                DeleteResponse.NotFound => Results.NotFound(),
                DeleteResponse.NotDeletableYet notYet => Results.Problem(
                    type: "urn:altinn:problem:minimum-persistence-lifetime-not-satisfied",
                    title: "Deletion not allowed during minimum persistence lifetime period",
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    detail: "The instance cannot be deleted yet because it is still within the configured minimum persistence lifetime period after archiving.",
                    instance: $"{settings.DialogportenAdapter.Altinn.GetPlatformUri()}instance/{instanceOwner}/{instanceGuid}",
                    extensions: new Dictionary<string, object?>
                    {
                        ["archivedAt"] = notYet.ArchivedAt.ToString("O"),
                        ["gracePeriodDays"] = notYet.GracePeriod,
                        ["deletionAllowedAt"] = notYet.DeletionAllowedAt.ToString("O")
                    }
                ),
                _ => Results.InternalServerError()
            };
        })
        .AllowAnonymous(); // 👈 Dialog token is validated inside InstanceService

    app.Run();
}

ILoggerFactory CreateBootstrapLoggerFactory() => LoggerFactory.Create(builder => builder
    .SetMinimumLevel(LogLevel.Warning)
    .AddSimpleConsole(options =>
    {
        options.IncludeScopes = true;
        options.UseUtcTimestamp = true;
        options.SingleLine = true;
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    }));

partial class Program
{
    [LoggerMessage(LogLevel.Critical, "Application terminated unexpectedly")]
    static partial void LogApplicationTerminatedUnexpectedly(ILogger<Program> logger, Exception exception);
}


using System.Net;
using Refit;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;

internal sealed partial class MockDialogportenApi : IDialogportenApi
{
    private static readonly RefitSettings _refitSettings = new();

    private readonly ILogger<MockDialogportenApi> _logger;

    public MockDialogportenApi(ILogger<MockDialogportenApi> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IApiResponse<DialogDto>> Get(Guid dialogId, CancellationToken cancellationToken = default)
    {
        Log.LogGetCalled(_logger, dialogId);
        var apiResponse = new ApiResponse<DialogDto>(
            response: new HttpResponseMessage(HttpStatusCode.NotFound),
            content: null,
            settings: _refitSettings,
            error: null);
        return Task.FromResult<IApiResponse<DialogDto>>(apiResponse);
    }

    public Task<Guid> Create(DialogDto dto, bool isSilentUpdate = false,
        CancellationToken cancellationToken = default)
    {
        Log.LogCreateCalled(_logger, dto);
        return Task.FromResult(Guid.Empty);
    }

    public Task<IApiResponse> Update(DialogDto dto, Guid revision, bool isSilentUpdate = false,
        CancellationToken cancellationToken = default)
    {
        Log.LogUpdateCalled(_logger, dto, revision);
        var apiResponse = new ApiResponse<object>(
            settings: _refitSettings,
            response: new HttpResponseMessage(HttpStatusCode.NoContent)
            {
                Headers = { ETag = new System.Net.Http.Headers.EntityTagHeaderValue($"\"{Guid.NewGuid()}\"") }
            },
            content: null,
            error: null);
        return Task.FromResult<IApiResponse>(apiResponse);
    }

    public Task Delete(Guid dialogId, Guid revision, bool isSilentUpdate = false,
        CancellationToken cancellationToken = default)
    {
        Log.LogDeleteCalled(_logger, dialogId, revision);
        return Task.CompletedTask;
    }

    public Task Purge(Guid dialogId, Guid revision, bool isSilentUpdate = false,
        CancellationToken cancellationToken = default)
    {
        Log.LogPurgeCalled(_logger, dialogId, revision);
        return Task.CompletedTask;
    }

    public Task<IApiResponse> Restore(Guid dialogId, Guid revision, bool isSilentUpdate = false,
        CancellationToken cancellationToken = default)
    {
        Log.LogRestoreCalled(_logger, dialogId, revision);
        var apiResponse = new ApiResponse<object>(
            response: new HttpResponseMessage(HttpStatusCode.NotFound),
            content: null,
            settings: _refitSettings,
            error: null);
        return Task.FromResult<IApiResponse>(apiResponse);
    }

    public Task<IApiResponse> UpdateFormSavedActivityTime(Guid dialogId, Guid activityId, Guid revision, DateTimeOffset newCreatedAt,
        CancellationToken cancellationToken = default)
    {
        Log.LogUpdateFormSavedActivityTimeCalled(_logger, dialogId, activityId, revision, newCreatedAt);
        var apiResponse = new ApiResponse<object>(
            response: new HttpResponseMessage(HttpStatusCode.NotFound),
            content: null,
            settings: _refitSettings,
            error: null);
        return Task.FromResult<IApiResponse>(apiResponse);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "MockDialogportenApi.Get called with dialogId: {DialogId}")]
        public static partial void LogGetCalled(ILogger logger, Guid dialogId);

        [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "MockDialogportenApi.Create called with dialog: {@Dialog}")]
        public static partial void LogCreateCalled(ILogger logger, DialogDto dialog);

        [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "MockDialogportenApi.Update called with dialog: {@Dialog}, revision: {Revision}")]
        public static partial void LogUpdateCalled(ILogger logger, DialogDto dialog, Guid revision);

        [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "MockDialogportenApi.Delete called with dialogId: {DialogId}, revision: {Revision}")]
        public static partial void LogDeleteCalled(ILogger logger, Guid dialogId, Guid revision);

        [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "MockDialogportenApi.Purge called with dialogId: {DialogId}, revision: {Revision}")]
        public static partial void LogPurgeCalled(ILogger logger, Guid dialogId, Guid revision);

        [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "MockDialogportenApi.Restore called with dialogId: {DialogId}, revision: {Revision}")]
        public static partial void LogRestoreCalled(ILogger logger, Guid dialogId, Guid revision);

        [LoggerMessage(EventId = 7, Level = LogLevel.Debug, Message = "MockDialogportenApi.UpdateFormSavedActivityTime called with dialogId: {DialogId}, activityId: {ActivityId}, revision: {Revision}, newCreatedAt: {NewCreatedAt}")]
        public static partial void LogUpdateFormSavedActivityTimeCalled(ILogger logger, Guid dialogId, Guid activityId, Guid revision, DateTimeOffset newCreatedAt);
    }
}


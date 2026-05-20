namespace Altinn.DialogportenAdapter.WebApi.Common.Exceptions;

public class DialogNotFoundForPurgeException(Guid dialogId, Guid revision, string traceId)
    : Exception($"Can't purge dialog {dialogId} at revision {revision}: Dialog not found. TraceId: {traceId}");

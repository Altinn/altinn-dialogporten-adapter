namespace Altinn.DialogportenAdapter.WebApi.Common.Exceptions;

public class DialogNotFoundForPurgeException(Guid dialogId, Guid revision)
    : Exception($"Can't purge dialog {dialogId} at revision {revision}: Dialog not found.");

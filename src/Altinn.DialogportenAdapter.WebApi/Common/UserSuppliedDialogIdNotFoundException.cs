namespace Altinn.DialogportenAdapter.WebApi.Common;

internal sealed class  UserSuppliedDialogIdNotFoundException(string instanceId): InvalidOperationException($"User supplied dialog id for instance {instanceId} could not be found.");

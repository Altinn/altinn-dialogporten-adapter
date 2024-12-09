namespace Altinn.Platform.DialogportenAdapter.WebApi.Features.Command.Sync;

public record SyncInstanceToDialogDto(
    string AppId,
    int PartyId, 
    Guid InstanceId,
    DateTimeOffset InstanceCreatedAt);


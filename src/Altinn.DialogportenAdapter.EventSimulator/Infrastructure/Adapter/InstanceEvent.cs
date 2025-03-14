namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Adapter;

public record InstanceEvent(
    string AppId,
    int PartyId, 
    Guid InstanceId,
    DateTimeOffset InstanceCreatedAt,
    bool IsMigration = true);
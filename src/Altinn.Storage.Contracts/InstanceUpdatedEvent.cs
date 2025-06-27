namespace Altinn.Storage.Contracts;

public record InstanceUpdatedEvent(
    string AppId,
    string PartyId,
    Guid InstanceId,
    DateTimeOffset InstanceCreatedAt,
    bool IsMigration);
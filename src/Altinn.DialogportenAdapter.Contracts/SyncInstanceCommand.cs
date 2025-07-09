using Wolverine.Attributes;

namespace Altinn.DialogportenAdapter.Contracts;

[MessageIdentity("Altinn.DialogportenAdapter.SyncInstanceCommand")]
public record SyncInstanceCommand(
    string AppId,
    string PartyId,
    Guid InstanceId,
    DateTimeOffset InstanceCreatedAt,
    bool IsMigration);
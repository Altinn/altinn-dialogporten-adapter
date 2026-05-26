using Wolverine.Attributes;

namespace Altinn.DialogportenAdapter.Contracts;

[MessageIdentity("Altinn.DialogportenAdapter.SyncInstanceCommand")]
// This is intentionally more than the HTTP timeout set in ServiceCollectionExtensions,
// where .AddRefitClient<IDialogportenApi>() sets the HTTP level timeout to 10 minutes,
// making it the effective limit. Note that messages holding a lock for more than
// 5 minutes will throw, as the max lock duration is 5 minutes in ASB. The message
// will still be processed, but might require manual intervention to clear from the
// queue to avoid needless retries.
[MessageTimeout(30 * 60)]
public record SyncInstanceCommand(
    string AppId,
    string PartyId,
    Guid InstanceId,
    DateTimeOffset InstanceCreatedAt,
    bool IsMigration);
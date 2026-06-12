using System.Globalization;
using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

namespace Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;

internal static class InstanceDtoExtensions
{
    public static SyncInstanceCommand ToSyncInstanceCommand(this InstanceDto instance, bool isMigration = true)
    {
        var (partyId, instanceId) = ParseInstanceId(instance.Id);
        return new SyncInstanceCommand(instance.AppId, partyId, instanceId, instance.Created, isMigration);
    }

    private static (string PartyId, Guid InstanceId) ParseInstanceId(ReadOnlySpan<char> id)
    {
        var partsEnumerator = id.Split("/");
        if (!partsEnumerator.MoveNext() || !int.TryParse(id[partsEnumerator.Current], out var party))
        {
            throw new InvalidOperationException("Invalid instance id");
        }

        if (!partsEnumerator.MoveNext() || !Guid.TryParse(id[partsEnumerator.Current], out var instance))
        {
            throw new InvalidOperationException("Invalid instance id");
        }

        return (party.ToString(CultureInfo.InvariantCulture), instance);
    }
}
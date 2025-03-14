using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Adapter;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

namespace Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;

internal static class InstanceDtoExtensions
{
    public static InstanceEvent ToInstanceEvent(this InstanceDto instance, bool isMigration = true)
    {
        var (partyId, instanceId) = ParseInstanceId(instance.Id);
        return new InstanceEvent(instance.AppId, partyId, instanceId, instance.Created, isMigration);
    }

    private static (int PartyId, Guid InstanceId) ParseInstanceId(ReadOnlySpan<char> id)
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

        return (party, instance);
    }
}
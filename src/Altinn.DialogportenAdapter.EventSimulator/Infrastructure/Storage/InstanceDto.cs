namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

internal sealed record InstanceDto(string AppId, string Id, DateTimeOffset Created, DateTimeOffset LastChanged);
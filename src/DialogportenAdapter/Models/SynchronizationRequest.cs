namespace DialogportenAdapter.Models;

public class SynchronizationRequest
{
    public required string ApplicationId { get; init; }
    public required string InstanceId { get; init; }
}

public record Lala(Guid ApplicationId, Guid InstanceId);
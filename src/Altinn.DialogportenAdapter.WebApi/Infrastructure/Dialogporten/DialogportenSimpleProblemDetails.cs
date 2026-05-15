using System.Text.Json.Serialization;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;

public class DialogportenSimpleProblemDetails
{
    [JsonPropertyName("status")] public required int Status { get; set; }
    [JsonPropertyName("traceId")] public required string TraceId { get; set; }
}

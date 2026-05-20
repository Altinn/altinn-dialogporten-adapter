using System.Text.Json;
using System.Text.Json.Serialization;
using Refit;

namespace Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;

public class DpSimpleProblemDetails
{
    [JsonPropertyName("status")] public required int Status { get; set; }
    [JsonPropertyName("traceId")] public required string TraceId { get; set; }

    public static DpSimpleProblemDetails? FromFailedApiResponseIf(
        IApiResponse response,
        Predicate<IApiResponse> condition
    )
    {
        if (response.IsSuccessful) return null;
        if (response.Error == null)
        {
            var method = response.RequestMessage?.Method;
            var uri = response.RequestMessage?.RequestUri;
            throw new InvalidOperationException($"Request failed and error is null: {method} {uri}");
        };

        if (condition.Invoke(response) && response.Error.HasContent)
        {
            var problem = JsonSerializer.Deserialize<DpSimpleProblemDetails>(response.Error.Content!);
            if (problem == null)
            {
                var method = response.RequestMessage?.Method;
                var uri = response.RequestMessage?.RequestUri;
                throw new InvalidOperationException($"Request failed and error deserialized to null: {method} {uri}");
            }

            return problem;
        }

        throw response.Error;
    }
}

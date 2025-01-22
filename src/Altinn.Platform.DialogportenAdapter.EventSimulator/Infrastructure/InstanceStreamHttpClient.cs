using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.DialogportenAdapter.EventSimulator.Infrastructure;

internal sealed class InstanceStreamHttpClient
{
    private readonly HttpClient _client;
    private readonly ILogger<InstanceStreamHttpClient> _logger;

    public InstanceStreamHttpClient(HttpClient client, ILogger<InstanceStreamHttpClient> logger)
    {
        _client = client;
        _logger = logger;
    }
    
    public async IAsyncEnumerable<InstanceEvent> GetInstanceStream(string appId, string token,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var next = (string?)$"/storage/api/v1/instances?pageSize=1000&appId={appId}";
        var auth = new AuthenticationHeaderValue("Bearer", token);
        while (next is not null)
        {
            InstanceQueryResponse? result;
            try
            {
                using var msg = new HttpRequestMessage(HttpMethod.Get, next);
                msg.Headers.Authorization = auth;
                using var response = await _client.SendAsync(msg, cancellationToken);
                result = await response.Content.ReadFromJsonAsync<InstanceQueryResponse>(cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to fetch instance stream.");
                yield break;
            }

            next = result?.Next;
            foreach (var instance in result?.Instances ?? [])
            {
                var (partyId, instanceId) = ParseInstanceId(instance.Id);
                yield return new InstanceEvent(instance.AppId, partyId, instanceId, instance.Created);
            }
        }
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
    
    private sealed class InstanceQueryResponse
    {
        public List<InstanceResponse> Instances { get; set; } = null!;
        public string? Next { get; set; }
    }

    private sealed class InstanceResponse
    {
        public string AppId { get; set; } = null!;
        public string Id { get; set; } = null!;
        public DateTimeOffset Created { get; set; }
    }
}

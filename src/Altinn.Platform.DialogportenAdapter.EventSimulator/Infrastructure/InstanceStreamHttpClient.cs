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
    
    public async IAsyncEnumerable<Instance> GetInstanceStream(string appId, string token,
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
                yield return instance;
            }
        }
    }
    
    private sealed class InstanceQueryResponse
    {
        public List<Instance> Instances { get; set; } = null!;
        public string? Next { get; set; }
    }

    internal sealed class Instance
    {
        public string AppId { get; set; } = null!;
        public string Id { get; set; } = null!;
        public DateTimeOffset Created { get; set; }
    }
}

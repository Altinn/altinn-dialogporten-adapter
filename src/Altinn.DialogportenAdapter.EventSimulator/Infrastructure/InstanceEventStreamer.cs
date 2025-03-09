using System.Runtime.CompilerServices;
using Altinn.DialogportenAdapter.EventSimulator.Common;

namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

internal sealed class InstanceEventStreamer
{
    private static readonly List<TimeSpan> BackoffDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10)
    ];
    
    private readonly IHttpClientFactory _clientFactory;
    private readonly ILogger<InstanceEventStreamer> _logger;
    
    public InstanceEventStreamer(IHttpClientFactory clientFactory, ILogger<InstanceEventStreamer> logger)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async IAsyncEnumerable<InstanceDto> InstanceHistoryStream(string appId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach(var instanceDto in InstanceStream(
                          appId: appId, 
                          sortOrder: SortOrder.Descending,
                          cancellationToken: cancellationToken))
        {
            yield return instanceDto;
        }
    }

    public async IAsyncEnumerable<InstanceDto> InstanceUpdateStream(
        string org,
        DateTimeOffset from,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var backoffHandler = new BackoffHandler(
            withJitter: true,
            startPosition: BackoffHandler.Position.Last,
            delays: BackoffDelays);
        while (!cancellationToken.IsCancellationRequested)
        {
            await foreach (var instanceDto in InstanceStream(
               org: org, 
               from: from, 
               sortOrder: SortOrder.Ascending,
               cancellationToken: cancellationToken))
            {
                backoffHandler.Reset();
                from = instanceDto.LastChanged > from ? instanceDto.LastChanged : from;
                yield return instanceDto;
            }
            _logger.LogDebug("Done fetching instances for {org}. New fetch in {delay} +- {jitter}%.", org, backoffHandler.Current, BackoffHandler.JitterPercentage);
            await backoffHandler.Delay(cancellationToken);
            backoffHandler.Next();
        }
    }

    private async IAsyncEnumerable<InstanceDto> InstanceStream(
        string? org = null,
        string? appId = null,
        string? partyId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        SortOrder sortOrder = SortOrder.Ascending,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (org is null && appId is null)
        {
            throw new ArgumentException("Org or AppId must be defined.");
        }

        var order = sortOrder switch
        {
            SortOrder.Ascending => "asc",
            SortOrder.Descending => "desc",
            _ => throw new ArgumentOutOfRangeException(nameof(sortOrder), sortOrder, null)
        };
        
        var client = _clientFactory.CreateClient(Constants.MaskinportenClientDefinitionKey);
        from ??= DateTimeOffset.MinValue;
        to ??= DateTimeOffset.MaxValue;
        
        var queryString = QueryString
            .Create("SortBy", $"{order}:lastChanged")
            .Add("pageSize", "1000")
            .Add("lastChanged", $"gt:{from.Value.ToUniversalTime():O}")
            .Add("lastChanged", $"lt:{to.Value.ToUniversalTime():O}")
            .AddIfNotNull("org", org)
            .AddIfNotNull("appId", appId)
            .AddIfNotNull("instanceOwner.partyId", partyId);
        var next = $"storage/api/v1/instances{queryString}";
        
        while (next is not null)
        {
            InstanceQueryResponse? result;
            try
            { 
                result = await client.GetFromJsonAsync<InstanceQueryResponse>(next, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to fetch instance stream.");
                yield break;
            }

            if (result is null)
            {
                _logger.LogWarning("No instance response from storage.");
                break;
            }

            next = result.Next;
            foreach (var instance in result.Instances)
            {
                yield return instance;
            }
        }
    }

    private sealed record InstanceQueryResponse(List<InstanceDto> Instances, string? Next);
    private enum SortOrder { Ascending, Descending }
}

internal sealed record InstanceDto(string AppId, string Id, DateTimeOffset Created, DateTimeOffset LastChanged);

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
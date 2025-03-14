using System.Globalization;
using System.Runtime.CompilerServices;
using Altinn.DialogportenAdapter.EventSimulator.Common;
using Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;

namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

internal sealed class InstanceStreamer
{
    private static readonly List<TimeSpan> BackoffDelays =
    [
        // TODO: Remove 1 sec when https://github.com/Altinn/altinn-storage/pull/654/files is in tt02
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10)
    ];
    
    private readonly IHttpClientFactory _clientFactory;
    private readonly ILogger<InstanceStreamer> _logger;
    
    public InstanceStreamer(IHttpClientFactory clientFactory, ILogger<InstanceStreamer> logger)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
               sortOrder: Order.Ascending,
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

    public async IAsyncEnumerable<InstanceDto> InstanceStream(
        string? org = null,
        string? appId = null,
        string? partyId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int pageSize = 100,
        Order sortOrder = Order.Ascending,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (org is null) ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        if (appId is null) ArgumentException.ThrowIfNullOrWhiteSpace(org);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var order = sortOrder switch
        {
            Order.Ascending => "asc",
            Order.Descending => "desc",
            _ => throw new ArgumentOutOfRangeException(nameof(sortOrder), sortOrder, null)
        };
        
        var client = _clientFactory.CreateClient(Constants.MaskinportenClientDefinitionKey);
        var queryString = QueryString
            .Create("order", $"{order}:lastChanged")
            .Add("size", pageSize.ToString(CultureInfo.InvariantCulture))
            .AddIf(from.HasValue, "lastChanged", $"gt:{from?.ToUniversalTime():O}")
            .AddIf(to.HasValue, "lastChanged", $"lt:{to?.ToUniversalTime():O}")
            .AddIf(org is not null, "org", org!)
            .AddIf(appId is not null, "org", appId!)
            .AddIf(partyId is not null, "instanceOwner.partyId", partyId!);
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
    public enum Order { Ascending, Descending }
}
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Altinn.DialogportenAdapter.EventSimulator.Common;
using Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;

namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

public sealed record InstanceDto(string AppId, string Id, DateTimeOffset Created, DateTimeOffset LastChanged);

public interface IInstanceStreamer
{
    IAsyncEnumerable<InstanceDto> InstanceUpdateStream(
        string org,
        DateTimeOffset from,
        CancellationToken cancellationToken);

    IAsyncEnumerable<InstanceDto> InstanceStream(
        string? org = null,
        string? appId = null,
        string? partyId = null,
        DateTimeOffset? from = null,
        [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords")] 
        DateTimeOffset? to = null,
        int pageSize = 100,
        Order sortOrder = Order.Ascending,
        CancellationToken cancellationToken = default);

    public enum Order { Ascending, Descending }
}

internal sealed partial class InstanceStreamer : IInstanceStreamer
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
               sortOrder: IInstanceStreamer.Order.Ascending,
               cancellationToken: cancellationToken))
            {
                backoffHandler.Reset();
                from = instanceDto.LastChanged > from ? instanceDto.LastChanged : from;
                yield return instanceDto;
            }
            LogDoneFetchingInstancesForOrg(org, backoffHandler.Current, BackoffHandler.JitterPercentage);
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
        IInstanceStreamer.Order sortOrder = IInstanceStreamer.Order.Ascending,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (org is null) ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        if (appId is null) ArgumentException.ThrowIfNullOrWhiteSpace(org);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var order = sortOrder switch
        {
            IInstanceStreamer.Order.Ascending => "asc",
            IInstanceStreamer.Order.Descending => "desc",
            _ => throw new ArgumentOutOfRangeException(nameof(sortOrder), sortOrder, null)
        };

        if (from > to) yield break;
        var client = _clientFactory.CreateClient(Constants.MaskinportenClientDefinitionKey);
        var queryString = QueryString
            .Create("order", $"{order}:lastChanged")
            .Add("MainVersionExclude", "0") // Force inclusion of all Altinn versions (1, 2, 3)
            .Add("size", pageSize.ToString(CultureInfo.InvariantCulture))
            .AddIf(from.HasValue, "lastChanged", $"gt:{from?.ToUniversalTime():O}")
            .AddIf(to.HasValue, "lastChanged", $"lte:{to?.ToUniversalTime():O}")
            .AddIf(org is not null, "org", org!)
            .AddIf(appId is not null, "appId", appId!)
            .AddIf(partyId is not null, "instanceOwner.partyId", partyId);

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
                LogFailedToFetchInstanceStream(e);
                yield break;
            }

            if (result is null)
            {
                LogNoInstanceResponseFromStorage();
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

    [LoggerMessage(LogLevel.Debug, "Done fetching instances for {org}. New fetch in {delay} +- {jitter}%.")]
    partial void LogDoneFetchingInstancesForOrg(string org, TimeSpan delay, double jitter);

    [LoggerMessage(LogLevel.Error, "Failed to fetch instance stream.")]
    partial void LogFailedToFetchInstanceStream(Exception exception);

    [LoggerMessage(LogLevel.Warning, "No instance response from storage.")]
    partial void LogNoInstanceResponseFromStorage();
}
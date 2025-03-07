// using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;
//
// namespace Altinn.DialogportenAdapter.EventSimulator.Common;
//
// internal sealed class TransferInstanceEventConsumer : ChannelConsumer<TransferInstanceEvent>
// {
//     private readonly IChannelPublisher<InstanceEvent> _channelPublisher;
//     private readonly IStorageApi _storageApi;
//
//     public TransferInstanceEventConsumer(IChannelPublisher<InstanceEvent> channelPublisher, ILogger<InstanceEventConsumer> logger, IStorageApi storageApi)
//         : base(logger, consumers: 5, capacity: 10)
//     {
//         _channelPublisher = channelPublisher ?? throw new ArgumentNullException(nameof(channelPublisher));
//         _storageApi = storageApi;
//     }
//
//     protected override async Task Consume(TransferInstanceEvent item, int taskNumber, CancellationToken cancellationToken)
//     {
//         Logger.LogInformation("{TaskNumber}: Consuming transfer event for {partyId}", taskNumber, item.PartyId);
//         // TODO: Fix this
//         await _channelPublisher.Publish(new InstanceEvent(item.PartyId, item.From), cancellationToken);
//
//         var apps = await _storageApi.GetApplications(cancellationToken);
//         var orgs = apps.Applications
//             .Select(x => x.Org)
//             .Distinct();
//         
//     }
// }
//
// internal record TransferInstanceEvent(int PartyId, DateTimeOffset From, DateTimeOffset To);
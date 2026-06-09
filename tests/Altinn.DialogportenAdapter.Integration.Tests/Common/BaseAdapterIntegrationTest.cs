using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.Integration.Tests.Common.Extensions;
using Altinn.DialogportenAdapter.Integration.Tests.Common.Services;
using Altinn.DialogportenAdapter.Test.Common.Builder;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using Altinn.Platform.Storage.Interface.Models;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Wolverine;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Altinn.DialogportenAdapter.Integration.Tests.Common;

public abstract class BaseAdapterIntegrationTest(DialogportenAdapterApplication app) : IAsyncLifetime
{
    private const int DefaultWaitSeconds = 20;
    private ServiceBusReceiver AdapterQueueDlqReceiver { get; set; } = null!;
    private readonly ConcurrentQueue<object> _unhandledEvents = new();

    public ValueTask InitializeAsync()
    {
        AdapterQueueDlqReceiver = CreateReceiver();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        // Dispose asap, so a test doesn't accidentally consume a fallback mapping
        await AdapterQueueDlqReceiver.DisposeAsync();

        app.DialogportenApi.LogUnhandledRequests(app.App.Logger, "DialogportenApi").ResetAllExceptFallbackMapping();
        app.RegisterApi.LogUnhandledRequests(app.App.Logger, "RegisterApi").ResetAllExceptFallbackMapping();
        app.AltinnApi.LogUnhandledRequests(app.App.Logger, "AltinnApi").ResetAllExceptFallbackMapping();
        app.StorageApi.LogUnhandledRequests(app.App.Logger, "StorageApi").ResetAllExceptFallbackMapping();

        GetSyncJobCompleteSignal().Reset();
        var clearCache = app.App.Services.GetRequiredService<IFusionCache>().ClearAsync().AsTask();
        var drainMessages = DrainLeftoverMessages();
        await Task.WhenAll(clearCache, drainMessages);

        GC.SuppressFinalize(this);
    }

    protected async Task<EventProcessingResult> SendAndWait<T>(T command, TimeSpan? timeout = null) where T : notnull
    {
        _unhandledEvents.Enqueue(command);
        var bus = app.StorageScope.ServiceProvider.GetRequiredService<IMessageBus>();
        var eventProcessingResult = GetEventProcessingResult(timeout);
        await bus.SendAsync(command);
        return await eventProcessingResult;
    }

    private async Task<ServiceBusReceivedMessage?> WaitForDlqMessage(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(DefaultWaitSeconds);
        var message = await AdapterQueueDlqReceiver.ReceiveMessageAsync(maxWait, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        if (message != null)
        {
            await AdapterQueueDlqReceiver.CompleteMessageAsync(message, CancellationToken.None);
            _unhandledEvents.TryDequeue(out _);
        }

        return message;
    }

    protected record EventProcessingResult(bool IsSuccess, ServiceBusReceivedMessage? DlqMessage)
    {
        public void ShouldBeSuccessful()
        {
            if (IsSuccess) return;

            var expectation = "Expected IsSuccess to be True, but found False";
            Assert.Fail(DlqMessage != null
                ? $"{expectation}. DlqMessage: {DlqMessage.DeadLetterReason} - {DlqMessage.DeadLetterErrorDescription}"
                : $"{expectation}. No dlq event either. Did we time out?");
        }
    }

    private async Task<EventProcessingResult> GetEventProcessingResult(TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(DefaultWaitSeconds);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var syncJob = GetSyncJobCompleteSignal().Completed.Task.WaitAsync(maxWait, cts.Token);
        var getDlqMessage = WaitForDlqMessage(maxWait, cts.Token);

        await Task.WhenAny(syncJob, getDlqMessage);

        if (getDlqMessage.IsFaulted)
        {
            await cts.CancelAsync();
            Assert.Fail("Unexpected error: WaitForDlqMessage should not throw");
        }

        if (getDlqMessage.IsCompletedSuccessfully && getDlqMessage.Result != null)
        {
            await cts.CancelAsync();
            return new EventProcessingResult(false, getDlqMessage.Result);
        }

        if (syncJob.IsCompletedSuccessfully)
        {
            await cts.CancelAsync();
            await IgnoreCancellation(getDlqMessage);
            _unhandledEvents.TryDequeue(out _);

            return new EventProcessingResult(true, null);
        }

        await cts.CancelAsync();

        return new EventProcessingResult(false, null);
    }

    private static async Task IgnoreCancellation(Task task)
    {
        try
        {
            await task;
        }
        catch (TaskCanceledException)
        {
            // Ignore
        }
    }

    private SyncCompletionSignal GetSyncJobCompleteSignal()
    {
        return app.AppScope.ServiceProvider.GetRequiredService<SyncCompletionSignal>();
    }

    protected sealed record Arrangement(
        string AppId,
        int PartyId,
        DateTimeOffset InstanceCreatedAt,
        Guid InstanceId,
        Guid DialogId
    );

    protected Arrangement ArrangeDefaults()
    {
        var testName = TestContext.Current.Test!.TestDisplayName;
        var appId = "ttd/formueinntekt-skattemelding-v2";
        var partyId = 2;
        var instanceCreatedAt = DateTimeOffset.UtcNow;
        var instanceId = Guid.NewGuid();
        var dialogId = instanceId.ToVersion7(instanceCreatedAt);

        app.DialogportenApi
            .Given(Request.Create().DpGetDialog(dialogId))
            .AtPriority(short.MaxValue - 1)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.NotFound));

        app.StorageApi
            .Given(Request.Create().StorageGetApplication(appId))
            .AtPriority(short.MaxValue - 1)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(
                    AltinnApplicationBuilder
                        .NewDefaultAltinnApplication()
                        .WithLastChangedBy(testName)
                        .Build()
                )));

        app.StorageApi
            .Given(Request.Create().StorageGetApplicationTexts(appId, "nb"))
            .AtPriority(short.MaxValue - 1)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(new TextResource
                {
                    Id = "1",
                    Org = "skd",
                    Language = "nb",
                    Resources = []
                })));
        app.StorageApi
            .Given(Request.Create().StorageGetApplicationTexts(appId, "nn"))
            .AtPriority(short.MaxValue - 1)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(new TextResource
                {
                    Id = "2",
                    Org = "skd",
                    Language = "nn",
                    Resources = []
                })));
        app.StorageApi
            .Given(Request.Create().StorageGetApplicationTexts(appId, "en"))
            .AtPriority(short.MaxValue - 1)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(new TextResource
                {
                    Id = "3",
                    Org = "skd",
                    Language = "en",
                    Resources = []
                })));

        app.StorageApi
            .Given(Request.Create().StorageGetInstance(partyId, instanceId))
            .AtPriority(short.MaxValue - 1)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(AltinnInstanceBuilder
                    .NewInProgressInstance()
                    .WithCreatedBy(testName)
                    .WithInstanceOwner(new InstanceOwner
                    {
                        PartyId = $"{partyId}",
                    })
                    .Build())));

        app.StorageApi
            .Given(Request.Create().StorageGetInstanceEvents(partyId, instanceId))
            .AtPriority(short.MaxValue - 1)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(new InstanceEventList
                {
                    InstanceEvents =
                    [
                        AltinnInstanceEventBuilder
                            .NewCreatedByPlatformUserInstanceEvent(1)
                            .WithAdditionalInfo(testName)
                            .Build(),
                        AltinnInstanceEventBuilder
                            .NewSubmittedByPlatformUserInstanceEvent(1)
                            .WithAdditionalInfo(testName)
                            .Build()
                    ]
                })));

        app.StorageApi
            .Given(Request.Create().StoragePutInstanceDatavalues(partyId, instanceId))
            .AtPriority(short.MaxValue - 1)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK));

        app.RegisterApi
            .Given(Request.Create().RegisterPostPartySearch())
            .AtPriority(short.MaxValue - 1)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody(JsonSerializer.Serialize(new PartyQueryResponse([
                    new PartyIdentifier(
                        PartyId: partyId,
                        PartyType: "organization",
                        PersonIdentifier: null,
                        OrganizationIdentifier: "12345678",
                        ExternalUrn: "urn:altinn:organization:identifier-no:12345678",
                        DisplayName: testName
                    )
                ]))));

        app.DialogportenApi
            .Given(Request.Create().DpPostDialog())
            .AtPriority(short.MaxValue - 1)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.Created)
                .WithHeader("ETag", Guid.NewGuid().ToString()));

        return new Arrangement(appId, partyId, instanceCreatedAt, instanceId, dialogId);
    }

    /// <summary>
    /// Drains all remaining messages from the previous test to prevent cross-test contamination.
    ///
    /// After WireMock is reset, any message still being processed will hit 501 → immediate DLQ.
    /// However, a message sitting in the scheduled state (ScheduleRetryIndefinitely) must wait for
    /// the emulator's SQL poll cycle before it gets delivered and can 501 → DLQ. That poll can take
    /// 10–60 s under load. We short-circuit by cancelling scheduled messages directly via their
    /// sequence numbers, which is instantaneous and bypasses the SQL poll entirely.
    /// Any message that was actively being processed at reset time still flows → 501 → DLQ normally.
    /// </summary>
    private async Task DrainLeftoverMessages()
    {
        if (_unhandledEvents.IsEmpty) return;
        var timeoutSeconds = 60;
        var start = DateTimeOffset.UtcNow;
        await using var dlqReceiver = CreateReceiver();
        while (!_unhandledEvents.IsEmpty)
        {
            var elapsed = DateTimeOffset.UtcNow - start;
            if (elapsed.Seconds > timeoutSeconds)
            {
                Assert.Fail(
                    $"Unable to drain dql: Timeout after {elapsed} seconds. This invalidates the whole test suite. Look for the test that failed to drain! {_unhandledEvents.Count} undrained messages");
            }

            var dlqMessage = await dlqReceiver.ReceiveMessageAsync(
                TimeSpan.FromSeconds(timeoutSeconds),
                TestContext.Current.CancellationToken
            );

            if (dlqMessage != null)
            {
                await dlqReceiver.CompleteMessageAsync(dlqMessage);
                _unhandledEvents.TryDequeue(out var unhandledEvent);
                TestContext.Current.TestOutputHelper!.WriteLine($"Warning: Drained event {unhandledEvent}");
            }
        }
    }

    private ServiceBusReceiver CreateReceiver()
    {
        return app.ServiceBusClient.CreateReceiver(
            Constants.AdapterQueueName,
            new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter,
                PrefetchCount = 0
            }
        );
    }
}

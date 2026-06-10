using System.Collections.Concurrent;
using System.Globalization;
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
        try
        {
            app.DialogportenApi.LogUnhandledRequests(app.App.Logger, "DialogportenApi").ResetAllExceptFallbackMapping();
            app.RegisterApi.LogUnhandledRequests(app.App.Logger, "RegisterApi").ResetAllExceptFallbackMapping();
            app.AltinnApi.LogUnhandledRequests(app.App.Logger, "AltinnApi").ResetAllExceptFallbackMapping();
            app.StorageApi.LogUnhandledRequests(app.App.Logger, "StorageApi").ResetAllExceptFallbackMapping();

            GetSyncJobCompleteSignal().Reset();
            await app.App.Services.GetRequiredService<IFusionCache>().ClearAsync().AsTask();
            await DrainLeftoverMessages();
        }
        finally
        {
            await AdapterQueueDlqReceiver.DisposeAsync();
            GC.SuppressFinalize(this);
        }
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
            _unhandledEvents.TryDequeue(out var handledEvent);
            Log($"Debug: Received event on DLQ {handledEvent}");
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
            await IgnoreCancellation(getDlqMessage, "Got success event");
            _unhandledEvents.TryDequeue(out var handledEvent);
            Log($"Debug: An event was successfully handled {handledEvent}");

            return new EventProcessingResult(true, null);
        }

        await cts.CancelAsync();
        await IgnoreCancellation(getDlqMessage, "No success or DLQ event");

        return new EventProcessingResult(false, null);
    }

    private static async Task IgnoreCancellation(Task task,  string cancellationReason)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            Log($"Debug: Ignored a task that was cancelled:  {cancellationReason}");
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
    /// Drains all remaining messages off the dlq.
    ///
    /// This is important for isolating each test, so that messages from the previous test doesn't affect the next.
    /// Draining just the dlq should be sufficient, because we reset the WireMock stubs so all apis return 501.
    /// This should make any lingering messages go to the dlq.
    /// The default LockDuration in the service bus is 1 minute.
    /// Thats why this method should have a generous timeout, in case any messages are locked by another receiver.
    /// </summary>
    private async Task DrainLeftoverMessages()
    {
        if (_unhandledEvents.IsEmpty) return;
        Log("Warning: Found unhandled messages, draining leftover messages");

        var timeoutSeconds = 65;
        var start = DateTimeOffset.UtcNow;
        while (!_unhandledEvents.IsEmpty)
        {
            var elapsed = DateTimeOffset.UtcNow - start;
            if (elapsed.Seconds > timeoutSeconds)
            {
                Assert.Fail(
                    $"Unable to drain dql: Timeout after {elapsed} seconds. This invalidates the whole test suite. Look for the test that failed to drain! {_unhandledEvents.Count} undrained messages");
            }

            var dlqMessage = await AdapterQueueDlqReceiver.ReceiveMessageAsync(
                TimeSpan.FromSeconds(timeoutSeconds / 3),
                TestContext.Current.CancellationToken
            );

            if (dlqMessage != null)
            {
                await AdapterQueueDlqReceiver.CompleteMessageAsync(dlqMessage);
                _unhandledEvents.TryDequeue(out var unhandledEvent);
                Log($"Warning: Drained event {unhandledEvent}");
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

    private static void Log(string message)
    {
        var time = DateTime.UtcNow.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        TestContext.Current.TestOutputHelper!.WriteLine($"{time} ({Environment.CurrentManagedThreadId}): {message}");
    }
}

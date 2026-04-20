using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Altinn.DialogportenAdapter.Contracts;
using Altinn.DialogportenAdapter.Integration.Tests.Common.Extensions;
using Altinn.DialogportenAdapter.Test.Common.Builder;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using Altinn.Platform.Storage.Interface.Models;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Wolverine;
using Xunit;

namespace Altinn.DialogportenAdapter.Integration.Tests.Common;

public abstract class BaseAdapterIntegrationTest(DialogportenAdapterApplication app) : IAsyncLifetime
{
    private ServiceBusReceiver HistoryQueueDlqReceiver { get; set; } = null!;
    private readonly ConcurrentQueue<object> _unhandledEvents = new();

    public ValueTask InitializeAsync()
    {
        HistoryQueueDlqReceiver = app.ServiceBusClient.CreateReceiver(
            Constants.AdapterQueueName,
            new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter,
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
            }
        );

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        app.DialogportenApi.LogUnhandledRequests(app.App.Logger, "DialogportenApi").ResetAllExceptFallbackMapping();
        app.RegisterApi.LogUnhandledRequests(app.App.Logger, "RegisterApi").ResetAllExceptFallbackMapping();
        app.AltinnApi.LogUnhandledRequests(app.App.Logger, "AltinnApi").ResetAllExceptFallbackMapping();
        app.StorageApi.LogUnhandledRequests(app.App.Logger, "StorageApi").ResetAllExceptFallbackMapping();

        await DrainLeftoverMessages();
        await HistoryQueueDlqReceiver.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    protected async Task Send<T>(T command) where T : notnull
    {
        var bus = app.StorageScope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.SendAsync(command);
        _unhandledEvents.Enqueue(command);
    }

    protected async Task<ServiceBusReceivedMessage?> WaitForDlqMessage(TimeSpan? timeoutSeconds = null)
    {
        var maxWait = timeoutSeconds ?? TimeSpan.FromSeconds(5);
        var message = await HistoryQueueDlqReceiver.ReceiveMessageAsync(maxWait, TestContext.Current.CancellationToken);

        if (message != null)
        {
            _unhandledEvents.TryDequeue(out _);
        }

        return message;
    }

    protected async Task<ILogEntry?> WaitForDialogPostedLogEntry(TimeSpan? timeout = null)
    {
        var entry = await Time.WaitUntilAsync(() =>
        {
            var entries = app.DialogportenApi.FindLogEntries(Request.Create().DpPostDialog());
            return entries.Count > 0 ? entries[0] : null;
        }, timeout ?? TimeSpan.FromSeconds(5));

        if (entry != null)
        {
            _unhandledEvents.TryDequeue(out _);
        }

        return entry;
    }

    protected async Task<DialogDto> WaitForDialogPostedOrFail()
    {
        var postDialog = WaitForDialogPostedLogEntry(TimeSpan.FromSeconds(6));
        var getDlqMessage = WaitForDlqMessage(TimeSpan.FromSeconds(5));
        await Task.WhenAny(postDialog, getDlqMessage);

        if (getDlqMessage.Result != null)
        {
            var reason = getDlqMessage.Result.DeadLetterErrorDescription;
            Assert.Fail($"Expected a dialog to be posted. But dlq got message instead: {reason}");
        }

        if (postDialog.Result == null)
        {
            Assert.Fail("Expected a dialog to be posted. Did you forget to set up a wiremock post dialog request?");
        }

        var statusCode = (int)postDialog.Result.ResponseMessage!.StatusCode!;
        if (statusCode != 201)
        {
            var dlqMessage = await getDlqMessage;
            if (dlqMessage != null)
            {
                var reason = dlqMessage.DeadLetterErrorDescription;
                Assert.Fail($"Expected post dialog to return status code 201, was {statusCode}. Dlq reason: {reason}");
            }

            Assert.Fail($"Expected a dlq message when statusCode != 201, code was {statusCode}");
        }

        var body = postDialog.Result.RequestMessage?.Body;
        if (body == null)
        {
            Assert.Fail("Expected a request with a body");
        }

        return JsonSerializer.Deserialize<DialogDto>(body)!;
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
        var appId = "skd/formueinntekt-skattemelding-v2";
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
                .WithBody(JsonSerializer.Serialize(AltinnApplicationBuilder.NewDefaultAltinnApplication().Build())));

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
                        AltinnInstanceEventBuilder.NewCreatedByPlatformUserInstanceEvent(1).Build(),
                        AltinnInstanceEventBuilder.NewSubmittedByPlatformUserInstanceEvent(1).Build()
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
                        DisplayName: "KRASS VIRKSOMHET"
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
    /// </summary>
    private async Task DrainLeftoverMessages()
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < timeoutAt && !_unhandledEvents.IsEmpty)
        {
            var message = await HistoryQueueDlqReceiver.ReceiveMessageAsync(
                TimeSpan.FromMilliseconds(50),
                TestContext.Current.CancellationToken
            );

            if (message != null)
            {
                _unhandledEvents.TryDequeue(out _);
            }
        }
    }
}

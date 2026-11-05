using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Wolverine;
using Wolverine.ErrorHandling;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public async Task ConfigureDialogportenAdapterServices_AggregatedHttpRequestExceptions_UseTheTransientRule()
    {
        await using var provider = BuildServiceProvider();
        var failures = provider.GetRequiredService<WolverineOptions>().Policies.Failures;

        var transientRule = RuleFor(failures, new HttpRequestException("Name or service not known"));
        var rule = RuleFor(failures, new AggregateException(
            new HttpRequestException("Name or service not known"),
            new HttpRequestException("Name or service not known")));

        transientRule.Should().NotBeNull();
        rule.Should().BeSameAs(transientRule);
    }

    [Fact]
    public async Task ConfigureDialogportenAdapterServices_AggregatedTaskCanceledExceptions_UseTheTransientRule()
    {
        await using var provider = BuildServiceProvider();
        var failures = provider.GetRequiredService<WolverineOptions>().Policies.Failures;

        var transientRule = RuleFor(failures, new TaskCanceledException());
        var rule = RuleFor(failures, new AggregateException(new TaskCanceledException(), new TaskCanceledException()));

        transientRule.Should().NotBeNull();
        rule.Should().BeSameAs(transientRule);
    }

    // One transient failure is enough to retry the message. Once it clears, a remaining failure
    // is thrown on its own and matches no rule.
    [Fact]
    public async Task ConfigureDialogportenAdapterServices_AggregateWithOneHttpRequestException_UsesTheTransientRule()
    {
        await using var provider = BuildServiceProvider();
        var failures = provider.GetRequiredService<WolverineOptions>().Policies.Failures;

        var transientRule = RuleFor(failures, new HttpRequestException());
        var rule = RuleFor(failures, new AggregateException(new HttpRequestException(), new InvalidOperationException()));

        transientRule.Should().NotBeNull();
        rule.Should().BeSameAs(transientRule);
    }

    // Matching no rule moves the message to the error queue on the first attempt.
    [Fact]
    public async Task ConfigureDialogportenAdapterServices_AggregatedNonTransientExceptions_MatchNoRule()
    {
        await using var provider = BuildServiceProvider();
        var failures = provider.GetRequiredService<WolverineOptions>().Policies.Failures;

        var rule = RuleFor(failures, new AggregateException(new InvalidOperationException(), new InvalidOperationException()));

        rule.Should().BeNull();
    }

    // Registers the adapter's services the way the app does, with placeholder settings. Nothing connects.
    private static ServiceProvider BuildServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationInsights:Enabled"] = "false",
                ["DialogportenAdapter:Dialogporten:BaseUri"] = "http://localhost",
                ["DialogportenAdapter:Altinn:BaseUri"] = "http://localhost",
                ["DialogportenAdapter:Altinn:InternalStorageEndpoint"] = "http://localhost",
                ["DialogportenAdapter:Altinn:InternalRegisterEndpoint"] = "http://localhost",
                ["DialogportenAdapter:Adapter:BaseUri"] = "http://localhost",
                ["DialogportenAdapter:Authentication:JwtBearerWellKnown"] = "http://localhost/.well-known/openid-configuration",
                ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
                ["WolverineSettings:ListenerCount"] = "3"
            })
            .Build();

        var environment = new HostingEnvironment
        {
            EnvironmentName = Environments.Production,
            ApplicationName = "Altinn.DialogportenAdapter.WebApi",
            ContentRootPath = AppContext.BaseDirectory
        };

        return new ServiceCollection()
            .AddLogging()
            .ConfigureDialogportenAdapterServices(configuration, environment, new Clock())
            .BuildServiceProvider();
    }

    // Picks the rule Wolverine applies on a first attempt: the first one that produces a continuation.
    private static FailureRule? RuleFor(FailureRuleCollection failures, Exception exception)
    {
        var envelope = new Envelope { Attempts = 1 };
        return failures.FirstOrDefault(rule => rule.TryCreateContinuation(exception, envelope, out _));
    }
}

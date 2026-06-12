using System.Diagnostics;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using AwesomeAssertions;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Extensions;

public class TaskExtentionsTest
{

    [Fact(Timeout = 449)]
    public async Task GetAwaiter_Should_Throw_If_Ex_Happens_After_OK()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await (ThrowAfter(new ArgumentException(), 250), OkAfter(200));
        });
    }

    [Fact(Timeout = 449)]
    public async Task GetAwaiter_Should_Throw_If_Ex_Happens_Before_OK()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await (ThrowAfter(new ArgumentException(), 200), OkAfter(250));
        });
    }

    [Fact(Timeout = 449)]
    public async Task GetAwaiter_Should_Throw_If_Ex_Is_Last_Argument_And_Happens_After_OK()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await (OkAfter(200), ThrowAfter(new ArgumentException(), 250));
        });
    }

    [Fact(Timeout = 449)]
    public async Task GetAwaiter_Should_Throw_If_Ex_Is_Last_Argument_And_Happens_Before_OK()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await (OkAfter(250), ThrowAfter(new ArgumentException(), 200));
        });
    }

    [Fact(Timeout = 449)]
    public async Task GetAwaiter_Should_Throw_Ex_With_Fastest_InnerType_Even_If_First_Argument()
    {
        var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
        {
            await (ThrowAfter(new ArgumentException(), 200), ThrowAfter(new UnreachableException(), 250));
        });

        ex.InnerException.Should().BeOfType<ArgumentException>();
        ex.InnerExceptions.Count.Should().Be(2);
        ex.InnerExceptions[0].Should().BeOfType<ArgumentException>();
        ex.InnerExceptions[1].Should().BeOfType<UnreachableException>();
    }

    [Fact(Timeout = 449)]
    public async Task GetAwaiter_Should_Throw_Ex_With_Fastest_InnerType_Even_If_Last_Argument()
    {
        var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
        {
            await (ThrowAfter(new ArgumentException(), 250), ThrowAfter(new UnreachableException(), 200));
        });

        ex.InnerException.Should().BeOfType<UnreachableException>();
        ex.InnerExceptions.Count.Should().Be(2);
        ex.InnerExceptions[0].Should().BeOfType<UnreachableException>();
        ex.InnerExceptions[1].Should().BeOfType<ArgumentException>();
    }

    [Fact(Timeout = 449)]
    public async Task WithAggregatedExceptions_Should_Throw_Regular_Exception_If_Only_One_Fails()
    {
        await Assert.ThrowsAsync<UnreachableException>(async () =>
        {
            await Task
                .WhenAll(OkAfter(200), ThrowAfter(new UnreachableException(), 250))
                .WithAggregatedExceptions();
        });
    }

    [Fact(Timeout = 449)]
    public async Task WithAggregatedExceptions_Should_Throw_Regular_Exception_If_Only_One_Fails_As_Last_Argument()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await Task
                .WhenAll(ThrowAfter(new ArgumentException(), 250), OkAfter(200))
                .WithAggregatedExceptions();
        });
    }

    [Fact(Timeout = 449)]
    public async Task WithAggregatedExceptions_Should_Throw_Exception_From_First_Argument_First()
    {
        var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
        {
            await Task
                .WhenAll(ThrowAfter(new ArgumentException(), 200), ThrowAfter(new UnreachableException(), 250))
                .WithAggregatedExceptions();
        });

        ex.InnerException.Should().BeOfType<ArgumentException>();
        ex.InnerExceptions.Count.Should().Be(2);
        ex.InnerExceptions[0].Should().BeOfType<ArgumentException>();
        ex.InnerExceptions[1].Should().BeOfType<UnreachableException>();
    }

    [Fact(Timeout = 449)]
    public async Task WithAggregatedExceptions_Should_Throw_Exception_From_First_Argument_First_Even_If_Slower()
    {
        var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
        {
            await Task
                .WhenAll(ThrowAfter(new ArgumentException(), 250), ThrowAfter(new UnreachableException(), 200))
                .WithAggregatedExceptions();
        });

        ex.InnerException.Should().BeOfType<ArgumentException>();
        ex.InnerExceptions.Count.Should().Be(2);
        ex.InnerExceptions[0].Should().BeOfType<ArgumentException>();
        ex.InnerExceptions[1].Should().BeOfType<UnreachableException>();
    }

    private static async Task<string> ThrowAfter(Exception exception, int delay)
    {
        await Task.Delay(delay);
        throw exception;
    }

    private static async Task<string> OkAfter(int delay)
    {
        await Task.Delay(delay);
        return "";
    }
}

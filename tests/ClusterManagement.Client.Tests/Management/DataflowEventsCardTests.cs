using System.Globalization;
using Bunit;
using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Client.Management.Components;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Tests.DataflowEvents;
using ClusterManagement.Public.DataflowEvents;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Client;

namespace ClusterManagement.Client.Tests.Management;

public class DataflowEventsCardTests
{
    private readonly IDataflowEventBroker _eventBroker = Substitute.For<IDataflowEventBroker>();

    [Fact]
    public async Task Counts_the_event_datapoints_and_the_writable_ones()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx, DataflowEventTestUtils.CreateInputEvent(), DataflowEventTestUtils.CreateOutputEvent(), DataflowEventTestUtils.CreateOutputEvent());

        // Act
        var sut = ctx.Render<DataflowEventsCard>(p => p.Add(c => c.ActiveVersion, new Version(0, 1, 1)));

        // Assert
        sut.WaitForAssertion(() => sut.Find(".content").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.DataflowEventCount, 3, 1)));
    }

    [Fact]
    public async Task Says_so_when_the_running_version_has_no_event_datapoints()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);

        // Act
        var sut = ctx.Render<DataflowEventsCard>(p => p.Add(c => c.ActiveVersion, new Version(0, 1, 1)));

        // Assert
        sut.WaitForAssertion(() => sut.Find(".content").TextContent.Should().Be(ClusterOverview.NoDataflowEvents));
    }

    [Fact]
    public async Task Counts_again_when_another_version_runs()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        var sut = ctx.Render<DataflowEventsCard>(p => p.Add(c => c.ActiveVersion, new Version(0, 1, 1)));
#pragma warning disable xUnit1051 // The card calls the overload without a token, so that is the one to set up.
        _eventBroker.GetEvents().Returns([DataflowEventTestUtils.CreateInputEvent()]);
#pragma warning restore xUnit1051

        // Act
        sut.Render(p => p.Add(c => c.ActiveVersion, new Version(0, 1, 2)));

        // Assert
        sut.WaitForAssertion(() => sut.Find(".content").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.DataflowEventCount, 1, 1)));
    }

    [Fact]
    public async Task Shows_a_dash_when_the_events_cannot_be_read()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
#pragma warning disable xUnit1051 // The card calls the overload without a token, so that is the one to set up.
        _eventBroker.GetEvents().Returns<IReadOnlyCollection<IDataflowEvent>>(_ => throw new TimeoutException());
#pragma warning restore xUnit1051

        // Act
        var sut = ctx.Render<DataflowEventsCard>(p => p.Add(c => c.ActiveVersion, new Version(0, 1, 1)));

        // Assert
        sut.WaitForAssertion(() => sut.Find(".content").TextContent.Should().Be("–"));
    }

    private void Setup(BunitContext ctx, params IDataflowEvent[] events)
    {
        ctx.SetupSuiteServices();
        ctx.Services.AddSingleton(_eventBroker);
        _eventBroker.GetEvents().Returns(events);
    }
}

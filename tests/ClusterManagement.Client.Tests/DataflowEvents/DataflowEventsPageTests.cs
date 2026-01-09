using Bunit;
using ClusterManagement.Client.DataflowEvents;
using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Public.DataflowEvents;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.Modules.Localization.Extensions;
using Sdk.Testing.Client;
using Xunit;

namespace ClusterManagement.Client.Tests.DataflowEvents;

public sealed class DataflowEventsPageTests
{
    private readonly IDataflowEventBroker _eventBroker = Substitute.For<IDataflowEventBroker>();
    private readonly IDataflowEvent _inputEvent = DataflowEventTestUtils.CreateInputEvent();
    private readonly IDataflowEvent _outputEvent = DataflowEventTestUtils.CreateOutputEvent();

    [Fact]
    public void Page_should_render()
    {
        // Arrange
        using var ctx = new TestContext();
        ctx.SetupSuiteServices(setup =>
        {
            setup.ClientMediator.SetupDataflowEventRequest(_inputEvent, _outputEvent);
            setup.Services.AddLocalization<ClusterManagementClientModule, ClusterManagementClientLocalizer>();
        });
        ctx.SetupDevExpressBlazor();
        ctx.Services.AddSingleton(_eventBroker);
        ctx.Services.AddSingleton<DataflowEventPageService>();

        // Act
        var sut = ctx.RenderComponent<DataflowEventsPage>();

        // Assert
        Assert.NotNull(sut);
    }
}

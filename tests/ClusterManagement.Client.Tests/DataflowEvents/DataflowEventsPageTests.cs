using Bunit;
using ClusterManagement.Client.DataflowEvents;
using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Public.DataflowEvents;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Modules.Localization.Extensions;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.Tables.SimpleTable.Extensions;
using ViciOne.Ui.Blazor.Components.Toolbar.Extensions;

namespace ClusterManagement.Client.Tests.DataflowEvents;

public sealed class DataflowEventsPageTests
{
    private readonly IDataflowEventBroker _eventBroker = Substitute.For<IDataflowEventBroker>();
    private readonly IDataflowEvent _inputEvent = DataflowEventTestUtils.CreateInputEvent();
    private readonly IDataflowEvent _outputEvent = DataflowEventTestUtils.CreateOutputEvent();

    [Fact]
    public async Task Page_should_render()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices(setup =>
        {
            setup.ClientMediator.SetupDataflowEventRequest(_inputEvent, _outputEvent);
            setup.Services.AddLocalization<ClusterManagementClientModule, ClusterManagementClientLocalizer>();
        });
        ctx.Services.AddSimpleTable();
        ctx.JSInterop.ConfigureSimpleTableJSInterop();
        ctx.Services.AddToolbar();
        ctx.Services.AddSingleton(_eventBroker);
        ctx.Services.AddSingleton<DataflowEventPageService>();

        // Act
        var sut = ctx.Render<DataflowEventsPage>();

        // Assert
        Assert.NotNull(sut);
    }
}

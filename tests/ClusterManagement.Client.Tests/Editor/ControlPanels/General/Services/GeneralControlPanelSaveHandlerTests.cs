using ClusterManagement.Client.Editor.ControlPanels.General.Services;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using ViciOne.Ui.ClusterEditor.Services;

namespace ClusterManagement.Client.Tests.Editor.ControlPanels.General.Services;

public sealed class GeneralControlPanelSaveHandlerTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddSingleton(_ => _mediator)
            .AddSingleton(_ => _settingsService)
            .AddScoped<IControlPanelSaveHandler<GeneralControlPanelState>, GeneralControlPanelSaveHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_update_settings_and_return_success()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var state = new GeneralControlPanelState();
        state.Initialize(new EditorSettings { UserId = "test-user" });
        var saveHandler = (GeneralControlPanelSaveHandler)serviceProvider
            .GetRequiredService<IControlPanelSaveHandler<GeneralControlPanelState>>();

        SetupSendAndSimulateEvent<AddOrUpdateClusterEditorSettings>(
                _mediator, saveHandler,
                cmd => new ClusterEditorSettingsChanged(null, CrudAction.Updated) { CorrelationId = cmd.CorrelationId });

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        await _mediator.Received(1).Send(Arg.Any<AddOrUpdateClusterEditorSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_return_error_when_update_fails()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var state = new GeneralControlPanelState();
        state.Initialize(new EditorSettings { UserId = "test-user" });
        var saveHandler = (GeneralControlPanelSaveHandler)serviceProvider
            .GetRequiredService<IControlPanelSaveHandler<GeneralControlPanelState>>();

        SetupSendAndSimulateEvent<AddOrUpdateClusterEditorSettings>(
                _mediator, saveHandler,
                cmd => new ClusterEditorSettingsChanged(null, CrudAction.Created, new ErrorInfo(100, "update failed")) { CorrelationId = cmd.CorrelationId });

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var errorResult = result.Should().BeOfType<SaveErrorResult>().Subject;
        errorResult.Message.Should().Be("update failed");
        errorResult.ErrorCode.Should().Be(100);
    }

    private static void SetupSendAndSimulateEvent<TCommand>(
        IUiMediator mediator,
        GeneralControlPanelSaveHandler service,
        Func<TCommand, ClusterEditorSettingsChanged> eventFactory)
        where TCommand : class, ICommand
        => mediator.When(m => m.Send(Arg.Any<TCommand>(), Arg.Any<CancellationToken>()))
            .Do(async callinfo =>
            {
                var command = callinfo.Arg<TCommand>();
                var @event = eventFactory(command);
                if (@event is ClusterEditorSettingsChanged change)
                {
                    var context = new ClientContext<ClusterEditorSettingsChanged>(change, command.CorrelationId);
                    await service.Consume(context, TestContext.Current.CancellationToken);
                }
            });
}

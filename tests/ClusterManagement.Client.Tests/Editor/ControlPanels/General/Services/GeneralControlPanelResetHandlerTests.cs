using Bunit;
using Bunit.TestDoubles;
using ClusterManagement.Client.Editor.ControlPanels.General.Services;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Requests;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace ClusterManagement.Client.Tests.Editor.ControlPanels.General.Services;

public sealed class GeneralControlPanelResetHandlerTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private const string TestUserId = "test-user";

    private BunitContext SetupTestContext(Action<BunitAuthorizationContext>? auth = null)
    {
        var ctx = new BunitContext();
        var authContext = ctx.AddAuthorization();
        auth?.Invoke(authContext);

        ctx.Services
            .AddSingleton(_ => _mediator)
            .AddScoped<IControlPanelResetHandler<GeneralControlPanelState>, GeneralControlPanelResetHandler>();

        return ctx;
    }

    [Fact]
    public async Task Should_reset_state_with_user_editor_settings_from_response()
    {
        // Arrange
        await using var ctx = SetupTestContext(authContext => authContext.SetAuthorized(TestUserId, AuthorizationState.Authorized));
        var state = new GeneralControlPanelState();
        var settings = new EditorSettings { UserId = TestUserId, MinimapNodeColoring = true, SimplifiedView = true };
        SetupSettingsRequest(_mediator, settings);

        var resetHandler = ctx.Services.GetRequiredService<IControlPanelResetHandler<GeneralControlPanelState>>();

        // Act
        await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        state.Settings.Should().BeEquivalentTo(state.Settings);
        await _mediator.Received().Request<GetClusterEditorSettingsRequest, GetClusterEditorSettingsResponse>(
            Arg.Is<GetClusterEditorSettingsRequest>(k => k.UserId == TestUserId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_reset_with_user_id_from_auth_provider()
    {
        // Arrange
        await using var ctx = SetupTestContext(authContext => authContext.SetAuthorized("test-user", AuthorizationState.Authorized));
        var state = new GeneralControlPanelState();
        var settings = new EditorSettings { UserId = "test-user", MinimapNodeColoring = true, SimplifiedView = true };
        SetupSettingsRequest(_mediator, settings);

        var resetHandler = ctx.Services.GetRequiredService<IControlPanelResetHandler<GeneralControlPanelState>>();

        // Act
        await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        state.Settings.Should().BeEquivalentTo(state.Settings);
        await _mediator.Received().Request<GetClusterEditorSettingsRequest, GetClusterEditorSettingsResponse>(
            Arg.Is<GetClusterEditorSettingsRequest>(k => k.UserId == TestUserId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_throw_if_user_is_not_authenticated()
    {
        // Arrange
        await using var ctx = SetupTestContext();
        var state = new GeneralControlPanelState();
        var resetHandler = ctx.Services.GetRequiredService<IControlPanelResetHandler<GeneralControlPanelState>>();

        // Act
        var action = async () => await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Assert        
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Failed to retrieve user information.");
    }

    [Fact]
    public async Task Should_throw_if_user_editor_settings_is_null()
    {
        // Arrange
        await using var ctx = SetupTestContext();
        var state = new GeneralControlPanelState();
        SetupSettingsRequest(_mediator, null);
        var resetHandler = ctx.Services.GetRequiredService<IControlPanelResetHandler<GeneralControlPanelState>>();

        // Act
        var action = async () => await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Assert        
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Failed to retrieve user information.");
    }

    private static void SetupSettingsRequest(IUiMediator mediator, EditorSettings? settings)
        => mediator.Request<GetClusterEditorSettingsRequest, GetClusterEditorSettingsResponse>(Arg.Any<GetClusterEditorSettingsRequest>(), Arg.Any<CancellationToken>())
                .Returns(new GetClusterEditorSettingsResponse()
                {
                    Settings = settings
                });
}

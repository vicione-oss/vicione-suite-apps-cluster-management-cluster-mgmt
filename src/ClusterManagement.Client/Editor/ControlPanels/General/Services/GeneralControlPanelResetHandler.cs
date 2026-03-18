using ClusterManagement.Internal.Requests;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace ClusterManagement.Client.Editor.ControlPanels.General.Services;

public sealed class GeneralControlPanelResetHandler(IUiMediator mediator, AuthenticationStateProvider stateProvider) : IControlPanelResetHandler<GeneralControlPanelState>
{
    public async Task Reset(GeneralControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();

        try
        {
            var auth = await stateProvider.GetAuthenticationStateAsync();
            var userId = auth.User.Identity?.Name;

            if (string.IsNullOrEmpty(userId))
                throw new InvalidOperationException("Failed to retrieve user information.");

            var request = new GetClusterEditorSettingsRequest(userId);
            var response = await mediator.Request<GetClusterEditorSettingsRequest, GetClusterEditorSettingsResponse>(request, cancellationToken);
            if (response.RequestError is not null)
                throw new InvalidOperationException($"Failed to retrieve editor settings: {response.RequestError.Message}");

            if (response.Settings is null)
                throw new InvalidOperationException("Received null editor settings from request.");

            state.Initialize(response.Settings);
        }
        finally
        {
            state.EndLoading();
        }
    }
}

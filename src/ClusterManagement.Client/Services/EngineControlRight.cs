using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Authorization;

namespace ClusterManagement.Client.Services;

/// <summary>
/// The Engine control feature of the module: starting, stopping and tearing down engines and resetting the deployment
/// state of a cluster. The pages offer these only with it; the backend does not check it.
/// </summary>
internal static class EngineControlRight
{
    public static string Policy { get; }
        = ModulePolicyProvider.GetPolicy<ClusterManagementClientModule>(AccessLevel.Full, Public.Constants.EngineControlFeature);

    public static async Task<bool> IsGranted(IAuthorizationService authorizationService, Task<AuthenticationState>? authenticationState)
        => authenticationState is { } state
            && (await authorizationService.AuthorizeAsync((await state).User, Policy)).Succeeded;
}

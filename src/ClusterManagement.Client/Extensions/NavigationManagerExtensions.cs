using ClusterManagement.Client.Management.Models;
using Microsoft.AspNetCore.Components;

namespace ClusterManagement.Client.Extensions;

internal static class NavigationManagerExtensions
{
    public static void NavigateToEditor(this NavigationManager manager, ClusterInfoModel _)
        => manager.NavigateTo(Public.Constants.EditorModuleRoute); // TODO - open cluster in readonly mode (infos in model!)

    public static void NavigateToManagement(this NavigationManager manager)
        => manager.NavigateTo(Public.Constants.ManagementModuleRoute);

    public static void NavigateToManagementEvents(this NavigationManager manager)
        => manager.NavigateTo(Public.Constants.ManagementEventsRoute);
}

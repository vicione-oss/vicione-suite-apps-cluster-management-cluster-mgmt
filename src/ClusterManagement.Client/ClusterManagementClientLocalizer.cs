using ClusterManagement.Client.Management.Localization;
using Sdk.Client.Modules.Localization;

namespace ClusterManagement.Client;

internal sealed class ClusterManagementClientLocalizer : IClientModuleLocalizer<ClusterManagementClientModule>
{
    public string? GetDescription() => Common.ModuleDescription;
    public string GetTitle() => Common.ModuleTitle;
}

using ClusterManagement.Public;
using Sdk.Client.NavTiles.Attributes;
using Sdk.Client.NavTiles.Components;

namespace ClusterManagement.Client.Management.NavTiles
{
    [InitialNavTile<ClusterManagementClientModule>(LinkTarget = Constants.ManagementModuleRoute)]
    public partial class ClusterManagementNavTile : NavTileBase
    {
    }
}

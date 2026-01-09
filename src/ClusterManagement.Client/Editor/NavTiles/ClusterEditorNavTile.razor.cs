using ClusterManagement.Public;
using Sdk.Client.NavTiles.Attributes;
using Sdk.Client.NavTiles.Components;

namespace ClusterManagement.Client.Editor.NavTiles;

[InitialNavTile<ClusterManagementClientModule>(LinkTarget = Constants.EditorModuleRoute, Id = Constants.EditorNavTileId)]
public partial class ClusterEditorNavTile : NavTileBase
{ }

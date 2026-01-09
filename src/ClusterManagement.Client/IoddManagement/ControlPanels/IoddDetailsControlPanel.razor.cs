using Sdk.Client.ControlPanels.Attributes;

namespace ClusterManagement.Client.IoddManagement.ControlPanels;

[ControlPanelCategory<IoddControlPanelCategoryDescriptor>]
public partial class IoddDetailsControlPanel
{
    private bool _isOverviewExpanded = true;
    private bool _isDetailsExpanded = true;
}

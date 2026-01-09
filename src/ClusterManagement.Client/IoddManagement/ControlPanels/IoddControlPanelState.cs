using Sdk.Client.ControlPanels.Services;

namespace ClusterManagement.Client.IoddManagement.ControlPanels;

public sealed class IoddControlPanelState : ControlPanelState
{
    public bool AutoDownload { get; set; }

    public TreeSelectionKey? SelectedTreeNodeKey { get; set; }
}

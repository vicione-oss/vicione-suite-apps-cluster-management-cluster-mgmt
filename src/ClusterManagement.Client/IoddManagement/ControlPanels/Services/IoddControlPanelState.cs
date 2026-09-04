using ClusterManagement.Client.IoddManagement.Models;
using ClusterManagement.Public.Iodds;
using Sdk.Client.ControlPanels.Services;

namespace ClusterManagement.Client.IoddManagement.ControlPanels.Services;

public sealed class IoddControlPanelState : ControlPanelState
{
    public List<IoddId> DevicesMarkedForDeletion { get; } = [];

    public string FilterText { get; set; } = string.Empty;

    public bool AutoDownload { get; set; }

    public bool WaitingForAutoDownloadChange { get; set; }

    public TreeSelectionKey? SelectedTreeNodeKey { get; set; }

    public List<DeviceView> Devices
    {
        get;
        internal set
        {
            if (value == field)
                return;

            field = value;

            OnPropertyChanged(nameof(Devices));
        }
    } = [];

    internal void MarkIoddAsDeleted(IEnumerable<IoddId> ids)
    {
        foreach (var id in ids)
        {
            if (DevicesMarkedForDeletion.Contains(id))
                continue;

            DevicesMarkedForDeletion.Add(id);
        }
    }

    internal bool HasChanges() => DevicesMarkedForDeletion.Count > 0;
}

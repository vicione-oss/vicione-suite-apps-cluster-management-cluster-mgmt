using ClusterManagement.Client.IoddManagement.Models;
using Sdk.Client.ControlPanels.Services;

namespace ClusterManagement.Client.IoddManagement.ControlPanels.Services;

public sealed class IoddDetailsControlPanelState : ControlPanelState
{
    public DeviceView? DeviceVariant { get; set; }
    public string DeviceImage { get; set; } = string.Empty;
}

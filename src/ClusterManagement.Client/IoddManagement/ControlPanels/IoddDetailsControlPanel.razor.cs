using ClusterManagement.Client.IoddManagement.ControlPanels.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.ControlPanels.Attributes;

namespace ClusterManagement.Client.IoddManagement.ControlPanels;

[ControlPanelCategory<IoddControlPanelCategoryDescriptor>]
public partial class IoddDetailsControlPanel
{
    private bool _isOverviewExpanded = true;
    private bool _isDetailsExpanded = true;

    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)] private TimeProvider TimeProvider { get; set; } = default!;
}

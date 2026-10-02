using ClusterManagement.Public.Connections.Contracts;
using Sdk.Client.Connections;
using ViciOne.Ui.Blazor.Components.ComboBox;

namespace ClusterManagement.Client.Connections;

/// <summary>
/// Renders the settings for an <see cref="OpcUaClientConnection"/> inside the suite's connection settings dialog.
/// </summary>
public sealed partial class OpcUaClientConnectionSettings : ConnectionSettingsComponentBase<OpcUaClientConnection>
{
    private readonly List<ComboBoxItem<OpcUaAuthenticationMode, string>> _authenticationModes = OpcUaConnectionComboBoxItems.AuthenticationModes();
}

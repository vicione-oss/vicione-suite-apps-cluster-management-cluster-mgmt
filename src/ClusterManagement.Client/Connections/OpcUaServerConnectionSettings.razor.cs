using ClusterManagement.Client.Connections.Localization;
using ClusterManagement.Public.Connections.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.Connections;
using Sdk.Client.Infrastructure;
using Sdk.SystemConfiguration.Requests;
using ViciOne.Ui.Blazor.Components.ComboBox;

namespace ClusterManagement.Client.Connections;

/// <summary>
/// Renders the settings for an <see cref="OpcUaServerConnection"/> inside the suite's connection settings dialog.
/// </summary>
public sealed partial class OpcUaServerConnectionSettings : ConnectionSettingsComponentBase<OpcUaServerConnection>
{
    private readonly List<ComboBoxItem<OpcUaAuthenticationMode, string>> _authenticationModes = OpcUaConnectionComboBoxItems.AuthenticationModes();

    private readonly List<ComboBoxItem<OpcUaSecurityPolicy, string>> _securityPolicies = OpcUaConnectionComboBoxItems.SecurityPolicies();

    private List<ComboBoxItem<string, string>> _networkInterfaces = [];

    [Inject] private IUiMediator Mediator { get; set; } = default!;

    [Inject] private ILogger<OpcUaServerConnectionSettings> Logger { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        var names = await GetEnabledNetworkInterfaceNames();

        // Keep a previously configured interface selectable even if the host no longer reports it,
        // so the combo box shows the stored value instead of silently appearing empty.
        if (!string.IsNullOrWhiteSpace(Connection.NetworkInterface))
            names.Add(Connection.NetworkInterface);

        names.RemoveAll(name => name == OpcUaServerConnection.LocalNetworkInterface);

        _networkInterfaces =
        [
            new() { Value = OpcUaServerConnection.LocalNetworkInterface, Text = OpcUaConnectionSettings.NetworkInterfaceLocal },
            .. OpcUaConnectionComboBoxItems.NetworkInterfaces(names),
        ];
    }

    private async Task<List<string>> GetEnabledNetworkInterfaceNames()
    {
        try
        {
            var response = await Mediator.Request<GetSystemConfiguration, GetSystemConfigurationResponse>(new GetSystemConfiguration(), CancellationToken.None);

            return response.Configuration?.NetworkInterfaces
                .Where(networkInterface => networkInterface.Enabled)
                .Select(networkInterface => networkInterface.Name)
                .ToList() ?? [];
        }
        catch (Exception e)
        {
            // Without the host's interfaces, still offer the local interface and the stored value.
            LogGetNetworkInterfacesError(Logger, e);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occurred while loading the network interfaces of the host.")]
    private static partial void LogGetNetworkInterfacesError(ILogger<OpcUaServerConnectionSettings> logger, Exception exception);
}

using ClusterManagement.Client.Connections.Localization;
using ClusterManagement.Public.Connections.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.Connections;
using Sdk.Client.Infrastructure;
using Sdk.SystemConfiguration.Requests;
using ViciOne.Ui.Blazor.Components.ComboBox;
using ViciOne.Ui.Blazor.Components.Dialog.Components;

namespace ClusterManagement.Client.Connections;

/// <summary>
/// Renders the settings for an <see cref="OpcUaServerConnection"/> inside the suite's connection settings dialog.
/// </summary>
public sealed partial class OpcUaServerConnectionSettings : ConnectionSettingsComponentBase<OpcUaServerConnection>
{
    private readonly List<ComboBoxItem<OpcUaAuthenticationMode, string>> _authenticationModes = OpcUaConnectionComboBoxItems.AuthenticationModes();

    private readonly List<ComboBoxItem<OpcUaSecurityPolicy, string>> _securityPolicies = OpcUaConnectionComboBoxItems.SecurityPolicies();

    private List<ComboBoxItem<string, string>> _networkInterfaces = [];

    private OpcUaCertificateStoreField? _applicationCertificateStore;
    private Dialog? _networkInterfaceChangedDialog;
    // the interface the application certificate was last accepted for, which an undo goes back to
    private string _acceptedNetworkInterface = string.Empty;

    [Inject] private IUiMediator Mediator { get; set; } = default!;

    [Inject] private ILogger<OpcUaServerConnectionSettings> Logger { get; set; } = default!;

    // A secured server checks client certificates against its trusted store.
    private bool RequiresTrustedClientCertificates => Connection.SecurityPolicy != OpcUaSecurityPolicy.None;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        _acceptedNetworkInterface = Connection.NetworkInterface;

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

    private async Task OnNetworkInterfaceChangedAsync()
    {
        await NotifyChanged();

        if (Connection.NetworkInterface == _acceptedNetworkInterface)
            return;

        // without an application certificate the DataPort creates one for the new interface anyway
        if (_applicationCertificateStore?.HasCertificates != true)
        {
            _acceptedNetworkInterface = Connection.NetworkInterface;
            return;
        }

        await _networkInterfaceChangedDialog!.ShowAsync();
    }

    private async Task DeleteApplicationCertificateAsync()
    {
        await _networkInterfaceChangedDialog!.CloseAsync();
        await _applicationCertificateStore!.DeleteAllAsync();
        _acceptedNetworkInterface = Connection.NetworkInterface;
    }

    private async Task KeepApplicationCertificateAsync()
    {
        await _networkInterfaceChangedDialog!.CloseAsync();
        _acceptedNetworkInterface = Connection.NetworkInterface;
    }

    private async Task UndoNetworkInterfaceChangeAsync()
    {
        await _networkInterfaceChangedDialog!.CloseAsync();
        Connection.NetworkInterface = _acceptedNetworkInterface;
        await NotifyChanged();
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

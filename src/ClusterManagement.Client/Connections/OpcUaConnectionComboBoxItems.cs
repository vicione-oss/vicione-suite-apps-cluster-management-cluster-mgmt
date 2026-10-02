using ClusterManagement.Client.Connections.Localization;
using ClusterManagement.Public.Connections.Contracts;
using ViciOne.Ui.Blazor.Components.ComboBox;

namespace ClusterManagement.Client.Connections;

/// <summary>
/// Builds the localized combo box item lists shared by the OPC UA client and server settings components.
/// </summary>
internal static class OpcUaConnectionComboBoxItems
{
    public static List<ComboBoxItem<OpcUaAuthenticationMode, string>> AuthenticationModes()
    => [
        new() { Value = OpcUaAuthenticationMode.Anonymous, Text = OpcUaConnectionSettings.AuthModeAnonymous },
        new() { Value = OpcUaAuthenticationMode.UsernamePassword, Text = OpcUaConnectionSettings.AuthModeUsernamePassword },
    ];

    public static List<ComboBoxItem<string, string>> NetworkInterfaces(IEnumerable<string> names)
        => [.. names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(name => new ComboBoxItem<string, string> { Value = name, Text = name })];

    public static List<ComboBoxItem<OpcUaSecurityPolicy, string>> SecurityPolicies()
    => [
        new() { Value = OpcUaSecurityPolicy.None, Text = OpcUaConnectionSettings.SecurityPolicyNone },
        new() { Value = OpcUaSecurityPolicy.Basic256Sha256Sign, Text = OpcUaConnectionSettings.SecurityPolicyBasic256Sha256Sign },
        new() { Value = OpcUaSecurityPolicy.Basic256Sha256SignAndEncrypt, Text = OpcUaConnectionSettings.SecurityPolicyBasic256Sha256SignAndEncrypt },
        new() { Value = OpcUaSecurityPolicy.Aes256Sha256RsaPssSignAndEncrypt, Text = OpcUaConnectionSettings.SecurityPolicyAes256Sha256RsaPssSignAndEncrypt },
    ];
}

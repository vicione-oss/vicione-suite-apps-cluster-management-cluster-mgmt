using System.Globalization;
using ClusterManagement.Client.Connections.Localization;
using ClusterManagement.Public.Connections.Contracts;
using Sdk.Client.Connections;

namespace ClusterManagement.Client.Connections;

/// <summary>
/// Validates the user-entered values of an <see cref="OpcUaServerConnection"/> shown in the connection settings dialog.
/// </summary>
public sealed class OpcUaServerConnectionValidator : ItemValidatorBase<OpcUaServerConnection>
{
    /// <inheritdoc />
    protected override void ValidateInternal(OpcUaServerConnection item)
    {
        if (string.IsNullOrWhiteSpace(item.ApplicationName))
            AddError(nameof(OpcUaServerConnection.ApplicationName), OpcUaConnectionSettings.ValidationApplicationNameRequired);

        if (string.IsNullOrWhiteSpace(item.ApplicationUri))
            AddError(nameof(OpcUaServerConnection.ApplicationUri), OpcUaConnectionSettings.ValidationApplicationUriRequired);

        if (string.IsNullOrWhiteSpace(item.Namespace))
            AddError(nameof(OpcUaServerConnection.Namespace), OpcUaConnectionSettings.ValidationNamespaceRequired);

        if (string.IsNullOrWhiteSpace(item.NetworkInterface))
            AddError(nameof(OpcUaServerConnection.NetworkInterface), OpcUaConnectionSettings.ValidationNetworkInterfaceRequired);

        if (string.IsNullOrWhiteSpace(item.Endpoint))
            AddError(nameof(OpcUaServerConnection.Endpoint), OpcUaConnectionSettings.ValidationEndpointRequired);

        var minInRange = ValidatePublishingInterval(
            item.MinPublishingIntervalMilliseconds,
            OpcUaServerConnection.MinPublishingIntervalLowerLimitMilliseconds,
            OpcUaServerConnection.MinPublishingIntervalUpperLimitMilliseconds,
            nameof(OpcUaServerConnection.MinPublishingIntervalMilliseconds));

        var maxInRange = ValidatePublishingInterval(
            item.MaxPublishingIntervalMilliseconds,
            OpcUaServerConnection.MaxPublishingIntervalLowerLimitMilliseconds,
            OpcUaServerConnection.MaxPublishingIntervalUpperLimitMilliseconds,
            nameof(OpcUaServerConnection.MaxPublishingIntervalMilliseconds));

        if (minInRange && maxInRange && item.MinPublishingIntervalMilliseconds > item.MaxPublishingIntervalMilliseconds)
        {
            AddError(nameof(OpcUaServerConnection.MinPublishingIntervalMilliseconds), OpcUaConnectionSettings.ValidationPublishingIntervalOrder);
        }

        if (item.AuthenticationMode == OpcUaAuthenticationMode.UsernamePassword)
        {
            if (string.IsNullOrWhiteSpace(item.Username))
                AddError(nameof(OpcUaServerConnection.Username), OpcUaConnectionSettings.ValidationUsernameRequired);

            if (string.IsNullOrWhiteSpace(item.Password))
                AddError(nameof(OpcUaServerConnection.Password), OpcUaConnectionSettings.ValidationPasswordRequired);
        }

        if (string.IsNullOrWhiteSpace(item.ApplicationCertificateSubject))
            AddError(nameof(OpcUaServerConnection.ApplicationCertificateSubject), OpcUaConnectionSettings.ValidationApplicationCertificateSubjectRequired);
    }

    private bool ValidatePublishingInterval(int value, int minimum, int maximum, string key)
    {
        if (value >= minimum && value <= maximum)
            return true;

        AddError(key, string.Format(CultureInfo.CurrentCulture, OpcUaConnectionSettings.ValidationPublishingIntervalRange, minimum, maximum));
        return false;
    }
}

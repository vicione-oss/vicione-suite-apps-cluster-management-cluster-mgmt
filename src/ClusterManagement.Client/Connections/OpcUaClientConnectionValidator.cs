using ClusterManagement.Client.Connections.Localization;
using ClusterManagement.Public.Connections.Contracts;
using Sdk.Client.Connections;

namespace ClusterManagement.Client.Connections;

/// <summary>
/// Validates the user-entered values of an <see cref="OpcUaClientConnection"/> shown in the connection settings dialog.
/// </summary>
public sealed class OpcUaClientConnectionValidator : ItemValidatorBase<OpcUaClientConnection>
{
    /// <inheritdoc />
    protected override void ValidateInternal(OpcUaClientConnection item)
    {
        if (string.IsNullOrWhiteSpace(item.ApplicationName))
            AddError(nameof(OpcUaClientConnection.ApplicationName), OpcUaConnectionSettings.ValidationApplicationNameRequired);

        if (string.IsNullOrWhiteSpace(item.ApplicationUri))
            AddError(nameof(OpcUaClientConnection.ApplicationUri), OpcUaConnectionSettings.ValidationApplicationUriRequired);

        if (string.IsNullOrWhiteSpace(item.Server))
            AddError(nameof(OpcUaClientConnection.Server), OpcUaConnectionSettings.ValidationServerRequired);

        if (item.Port is < 1 or > 65535)
            AddError(nameof(OpcUaClientConnection.Port), OpcUaConnectionSettings.ValidationPortRange);

        if (string.IsNullOrWhiteSpace(item.Endpoint))
            AddError(nameof(OpcUaClientConnection.Endpoint), OpcUaConnectionSettings.ValidationEndpointRequired);

        if (item.SubscriptionPublishingIntervalMilliseconds <= 0)
            AddError(nameof(OpcUaClientConnection.SubscriptionPublishingIntervalMilliseconds), OpcUaConnectionSettings.ValidationPublishingIntervalPositive);

        if (item.AuthenticationMode == OpcUaAuthenticationMode.UsernamePassword)
        {
            if (string.IsNullOrWhiteSpace(item.Username))
                AddError(nameof(OpcUaClientConnection.Username), OpcUaConnectionSettings.ValidationUsernameRequired);

            if (string.IsNullOrWhiteSpace(item.Password))
                AddError(nameof(OpcUaClientConnection.Password), OpcUaConnectionSettings.ValidationPasswordRequired);
        }

        if (string.IsNullOrWhiteSpace(item.ApplicationCertificateSubject))
            AddError(nameof(OpcUaClientConnection.ApplicationCertificateSubject), OpcUaConnectionSettings.ValidationApplicationCertificateSubjectRequired);
    }
}

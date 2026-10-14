namespace ClusterManagement.Internal.Contracts;

/// <summary>
/// A setting of an OPC UA connection an uploaded application certificate does not match.
/// </summary>
public enum OpcUaApplicationCertificateWarning
{
    /// <summary>The OPC UA stack finds the certificate by the application certificate subject, so it may create its own one instead.</summary>
    SubjectMismatch,

    /// <summary>The OPC UA stack checks the certificate's application URI against the connection's, so it may reject the certificate.</summary>
    ApplicationUriMismatch,
}

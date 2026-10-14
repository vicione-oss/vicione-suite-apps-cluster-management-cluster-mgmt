namespace ClusterManagement.Internal.Contracts;

/// <summary>Marker type matching the settings field file upload component to the OPC UA certificate upload handler.</summary>
public sealed class OpcUaCertificateUpload
{
    /// <summary>The directory below the module's cache that uploaded certificates are stored in until they are added to a store.</summary>
    public const string DirectoryName = "newUaCertificate";
}

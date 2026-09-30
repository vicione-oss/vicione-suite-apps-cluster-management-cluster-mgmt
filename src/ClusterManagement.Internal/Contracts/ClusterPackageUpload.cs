namespace ClusterManagement.Internal.Contracts;

/// <summary>Marker type matching the settings field file upload component to the cluster package upload handler.</summary>
public sealed class ClusterPackageUpload
{
    /// <summary>The directory below the module's cache that uploaded package archives are stored in.</summary>
    public const string DirectoryName = "newPackage";
}

namespace ClusterManagement.Internal.Contracts;

/// <summary>Marker type matching the settings field file upload component to the IODD upload handler.</summary>
public sealed class IoddUpload
{
    /// <summary>The directory below the module's cache that uploaded IODD archives are stored in.</summary>
    public const string DirectoryName = "newIodd";
}

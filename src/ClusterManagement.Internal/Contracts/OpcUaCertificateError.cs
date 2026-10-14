namespace ClusterManagement.Internal.Contracts;

/// <summary>
/// Error codes of the OPC UA certificate requests.
/// </summary>
public static class OpcUaCertificateError
{
    public const int UnknownError = 0;
    public const int InvalidStorePath = 600;
    public const int UploadedFileOutsideUploadDirectory = 601;
    public const int UploadedFileNotFound = 602;
    public const int InvalidCertificate = 603;
    public const int CertificateNotFound = 604;
    public const int NoPrivateKey = 605;
    public const int InvalidApplicationCertificate = 606;
}

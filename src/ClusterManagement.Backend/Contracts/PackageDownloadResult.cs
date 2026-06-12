namespace ClusterManagement.Backend.Contracts;

public record PackageDownloadResult(string SourcePath, string TargetPath) : HttpLoaderResult(SourcePath, TargetPath)
{
    public required string PackageName { get; init; }

    public required string PackageVersion { get; init; }
}

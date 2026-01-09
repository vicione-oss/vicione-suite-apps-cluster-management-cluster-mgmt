namespace ClusterManagement.Backend.Contracts;

public record HttpLoaderResult(string SourcePath, string TargetPath)
{
    public Exception? Error { get; set; }
    public bool Skipped { get; set; }
}

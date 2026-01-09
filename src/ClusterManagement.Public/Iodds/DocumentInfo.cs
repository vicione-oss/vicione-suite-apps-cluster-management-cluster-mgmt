namespace ClusterManagement.Public.Iodds;

public sealed class DocumentInfo
{
    public required DateOnly? ReleaseDate { get; init; }
    public required string Copyright { get; init; }
    public required string Version { get; init; }
}

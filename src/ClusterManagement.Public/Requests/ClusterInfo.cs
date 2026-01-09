namespace ClusterManagement.Public.Requests;

public sealed record ClusterInfo(Guid Id, Version Version, string Name, bool IsActive = false)
{
    public string? Description { get; init; }
}

public sealed class ClusterInfoSorter : IComparer<ClusterInfo>
{
    public int Compare(ClusterInfo? x, ClusterInfo? y)
    {
        if (x is null)
            return -1;

        if (y is null)
            return 1;

        if (!Equals(x.Id, y.Id))
            return x.Id.CompareTo(y.Id);// asc

        // descending
        return y.Version.CompareTo(x.Version);
    }
}

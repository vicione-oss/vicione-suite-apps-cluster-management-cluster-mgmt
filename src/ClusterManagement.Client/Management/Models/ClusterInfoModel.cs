using ClusterManagement.Public.Requests;

namespace ClusterManagement.Client.Management.Models;

public sealed class ClusterInfoModel(ClusterInfo info) : IComparable<ClusterInfoModel>, IEquatable<ClusterInfoModel>
{
    private readonly ClusterInfo _clusterInfo = info;

    /// <summary>
    /// Wrap the original cluster to avoid its IEquatable 
    /// </summary>
    public Version Version => _clusterInfo.Version;

    public Guid Id => _clusterInfo.Id;

    public string DisplayName => _clusterInfo.Name;

    public string DisplayNameVersion => $"{_clusterInfo.Name} v{_clusterInfo.Version}";

    public string? Description => _clusterInfo.Description;

    /// <summary>
    /// Unique per cluster version, for example as the key of its row in the version list
    /// </summary>
    public string Key => $"{Id}-{Version}";

    public DateTimeOffset? Deployed => _clusterInfo.Deployed;

    public bool IsActive { get; } = info.IsActive;
    public bool IsDefault { get; } = info.Version == Public.Constants.DefaultClusterVersion;
    public bool IsLatest { get; set; }

    public int CompareTo(ClusterInfoModel? other)
    {
        if (other is null)
            return -1;

        if (!Equals(Id, other.Id))
            return Id.CompareTo(other.Id);// asc

        // descending
        return other.Version.CompareTo(Version);
    }

    public bool Equals(ClusterInfoModel? other)
        => Equals(Key, other?.Key);

    public bool IsMatch(Guid? clusterId, Version? version)
        => Equals(Id, clusterId)
            && Equals(Version, version);

    public override bool Equals(object? obj)
        => Equals(obj as ClusterInfoModel);

    public override int GetHashCode()
        => HashCode.Combine(Id, Version);

    public static bool operator ==(ClusterInfoModel? left, ClusterInfoModel? right)
    {
        if (left is null)
            return right is null;

        return left.Equals(right);
    }

    public static bool operator !=(ClusterInfoModel? left, ClusterInfoModel? right)
        => !(left == right);

    public static bool operator <(ClusterInfoModel left, ClusterInfoModel right)
        => left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator <=(ClusterInfoModel left, ClusterInfoModel right)
        => left is null || left.CompareTo(right) <= 0;

    public static bool operator >(ClusterInfoModel left, ClusterInfoModel right)
        => left is not null && left.CompareTo(right) > 0;

    public static bool operator >=(ClusterInfoModel left, ClusterInfoModel right)
        => left is null ? right is null : left.CompareTo(right) >= 0;
}

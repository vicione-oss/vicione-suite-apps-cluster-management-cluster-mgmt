namespace ClusterManagement.Backend.Extensions;

internal static class HashCodeExtensions
{
    internal static void AddNullable<T>(this ref HashCode hashCode, T? value, IEqualityComparer<T>? comparer = default)
    {
        if (value is not null)
            hashCode.Add(value, comparer);
    }

    internal static void AddRange<T>(this ref HashCode hashCode, IEnumerable<T> values, IEqualityComparer<T>? comparer = default)
    {
        foreach (var value in values)
            hashCode.Add(value, comparer);
    }
}

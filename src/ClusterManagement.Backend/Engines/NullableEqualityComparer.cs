namespace ClusterManagement.Backend.Engines;

internal abstract class NullableEqualityComparer<T> : EqualityComparer<T>
{
    public sealed override bool Equals(T? x, T? y)
    {
        if (x is null && y is null)
            return true;
        else if (x is null || y is null)
            return false;
        return CoreEquals(x, y);
    }

    private protected abstract bool CoreEquals(T x, T y);
}

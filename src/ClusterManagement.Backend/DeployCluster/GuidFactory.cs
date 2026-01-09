namespace ClusterManagement.Backend.DeployCluster;

internal sealed class GuidFactory
{
    private static readonly int[] s_byteOrder = [15, 14, 13, 12, 11, 10, 9, 8, 6, 7, 4, 5, 0, 1, 2, 3];

    private Guid _guid;

    internal GuidFactory() : this(Guid.NewGuid()) { }
    internal GuidFactory(Guid guid) => _guid = guid;

    /// <remarks>https://stackoverflow.com/questions/30404965/increment-guid-in-c-sharp</remarks>
    internal Guid Next()
    {
        var bytes = _guid.ToByteArray();
        var canIncrement = s_byteOrder.Any(i => ++bytes[i] != 0);
        _guid = new Guid(canIncrement ? bytes : new byte[16]);
        return _guid;
    }

    internal Guid Reverse()
    {
        var bytes = _guid.ToByteArray();
        Array.Reverse(bytes);
        _guid = new Guid(bytes);
        return _guid;
    }
}

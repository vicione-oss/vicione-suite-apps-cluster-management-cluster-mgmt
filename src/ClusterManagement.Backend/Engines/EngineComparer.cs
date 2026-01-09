using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Engines;

internal sealed class EngineComparer : NullableEqualityComparer<Engine>
{
    private protected override bool CoreEquals(Engine x, Engine y) =>
        EqualityComparer<int>.Default.Equals(x.ChainIndex, y.ChainIndex) &&
        EqualityComparer<uint>.Default.Equals(x.CycleTime, y.CycleTime) &&
        EqualityComparer<Guid>.Default.Equals(x.Id, y.Id) &&
        EqualityComparer<LogLevel>.Default.Equals(x.LogLevel, y.LogLevel) &&
        StringComparer.Ordinal.Equals(x.Name, y.Name);

    public override int GetHashCode(Engine obj)
    {
        HashCode hash = new();
        hash.Add(obj.ChainIndex);
        hash.Add(obj.CycleTime);
        hash.Add(obj.Id);
        hash.Add(obj.LogLevel);
        hash.Add(obj.Name, StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}

using ClusterManagement.Backend.Extensions;
using ViciOne.Core.Contracts.DataModel;

namespace ClusterManagement.Backend.Engines;

internal sealed class FunctionBlockConnectorComparer : NullableEqualityComparer<FunctionBlockConnector>
{
    private protected override bool CoreEquals(FunctionBlockConnector x, FunctionBlockConnector y)
        => new TypedObjectComparer().Equals(x.CrossSourceAggregatingPooling, y.CrossSourceAggregatingPooling) &&
        EqualityComparer<PoolingStrategy?>.Default.Equals(x.CrossSourcePoolingStrategy, y.CrossSourcePoolingStrategy) &&
        EqualityComparer<Guid>.Default.Equals(x.DesignId, y.DesignId) &&
        EqualityComparer<Guid>.Default.Equals(x.Id, y.Id) &&
        new TypedObjectComparer().Equals(x.InitialValue, y.InitialValue) &&
        EqualityComparer<bool>.Default.Equals(x.MarkAsChangedOnlyIfNotEqual, y.MarkAsChangedOnlyIfNotEqual) &&
        new TypedObjectComparer().Equals(x.PerSourceAggregatingPooling, y.PerSourceAggregatingPooling) &&
        EqualityComparer<PoolingStrategy?>.Default.Equals(x.PerSourcePoolingStrategy, y.PerSourcePoolingStrategy) &&
        new TypedObjectComparer().Equals(x.Validator, y.Validator);

    public override int GetHashCode(FunctionBlockConnector obj)
    {
        HashCode hash = new();
        var typedObjectComparer = new TypedObjectComparer();
        hash.AddNullable(obj.CrossSourceAggregatingPooling, typedObjectComparer);
        hash.Add(obj.CrossSourcePoolingStrategy);
        hash.Add(obj.DesignId);
        hash.Add(obj.Id);
        hash.AddNullable(obj.InitialValue, typedObjectComparer);
        hash.Add(obj.MarkAsChangedOnlyIfNotEqual);
        hash.AddNullable(obj.PerSourceAggregatingPooling, typedObjectComparer);
        hash.Add(obj.PerSourcePoolingStrategy);
        hash.AddNullable(obj.Validator, typedObjectComparer);
        return hash.ToHashCode();
    }
}

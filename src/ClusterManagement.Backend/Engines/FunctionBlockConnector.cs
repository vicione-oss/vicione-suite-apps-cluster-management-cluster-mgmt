using ViciOne.Core.Contracts.DataModel;

namespace ClusterManagement.Backend.Engines;

public sealed class FunctionBlockConnector
{
    public Guid Id { get; set; }
    public Guid DesignId { get; set; }
    public TypedObject? InitialValue { get; set; }
    public PoolingStrategy? CrossSourcePoolingStrategy { get; set; }
    public TypedObject? CrossSourceAggregatingPooling { get; set; }
    public PoolingStrategy? PerSourcePoolingStrategy { get; set; }
    public TypedObject? PerSourceAggregatingPooling { get; set; }
    public bool MarkAsChangedOnlyIfNotEqual { get; set; }
    public TypedObject? Validator { get; set; }
}

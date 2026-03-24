namespace ClusterManagement.Backend.Tests;

internal partial class ClusterSystemTestContext
{
    internal void Given_user_added_FunctionBlock(Guid id, Guid? engineId = default)
    {
        var dataflow = _clusterBuilder.Cluster.Dataflows.First();
        var functionBlock = _clusterBuilder.Editors.Container.AddFunctionBlock(dataflow.Root, DummyFunctionBlock.DesignGuid, id.ToString(), id);
        functionBlock.Inputs.Clear();
        functionBlock.Outputs.Clear();
        if (engineId.HasValue)
        {
            var engine = _clusterBuilder.Cache.EngineIds[engineId.Value];
            _clusterBuilder.Editors.FunctionBlock.AssignEngine(engine, functionBlock);
        }
    }
}

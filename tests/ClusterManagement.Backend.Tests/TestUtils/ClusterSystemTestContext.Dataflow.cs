namespace ClusterManagement.Backend.Tests;

internal partial class ClusterSystemTestContext
{
    internal void Given_user_added_FunctionBlock(Guid id, Guid? engineId = default)
    {
        var dataflow = ClusterBuilder.Cluster.Dataflows.First();
        var functionBlock = ClusterBuilder.Editors.Container.AddFunctionBlock(dataflow.Root, DummyFunctionBlock.DesignGuid, id.ToString(), id);
        functionBlock.Inputs.Clear();
        functionBlock.Outputs.Clear();
        if (engineId.HasValue)
        {
            var engine = ClusterBuilder.Cache.EngineIds[engineId.Value];
            ClusterBuilder.Editors.FunctionBlock.AssignEngine(engine, functionBlock);
        }
    }
}

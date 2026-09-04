using ClusterManagement.Backend.Extensions;

namespace ClusterManagement.Backend.Engines;

internal sealed class WorkflowComparer : NullableEqualityComparer<Workflow>
{
    private protected override bool CoreEquals(Workflow x, Workflow y)
        => x.ClusterDependencies.OrderBy(x => x.Name).SequenceEqual(y.ClusterDependencies.OrderBy(x => x.Name)) &&
        x.Containers.OrderBy(x => x.Id).SequenceEqual(y.Containers.OrderBy(x => x.Id), new ContainerComparer()) &&
        new EngineComparer().Equals(x.Engine, y.Engine) &&
        new CommunicationConfigurationComparer().Equals(x.EngineLogCommunication, y.EngineLogCommunication) &&
        x.FunctionBlockConnectorLinks.OrderBy(x => x.Id).SequenceEqual(y.FunctionBlockConnectorLinks.OrderBy(x => x.Id), new FunctionBlockConnectorLinkComparer()) &&
        x.FunctionBlocks.OrderBy(x => x.Id).SequenceEqual(y.FunctionBlocks.OrderBy(x => x.Id), new FunctionBlockComparer()) &&
        x.ExternalConnections.SequenceEqual(y.ExternalConnections, new ExternalConnectionComparer()) &&
        new MqttCommunicationComparer().Equals(x.PersistenceCommunication, y.PersistenceCommunication) &&
        new MqttCommunicationComparer().Equals(x.RuntimeFunctionsCommunication, y.RuntimeFunctionsCommunication);

    public override int GetHashCode(Workflow obj)
    {
        HashCode hash = new();
        hash.AddRange(obj.ClusterDependencies.OrderBy(x => x.Name));
        hash.AddRange(obj.Containers.OrderBy(x => x.Id), new ContainerComparer());
        hash.Add(obj.Engine, new EngineComparer());
        hash.Add(obj.EngineLogCommunication, new CommunicationConfigurationComparer());
        hash.AddRange(obj.FunctionBlockConnectorLinks.OrderBy(x => x.Id), new FunctionBlockConnectorLinkComparer());
        hash.AddRange(obj.FunctionBlocks.OrderBy(x => x.Id), new FunctionBlockComparer());
        hash.AddRange(obj.ExternalConnections, new ExternalConnectionComparer());
        hash.Add(obj.PersistenceCommunication, new MqttCommunicationComparer());
        hash.Add(obj.RuntimeFunctionsCommunication, new MqttCommunicationComparer());
        return hash.ToHashCode();
    }
}

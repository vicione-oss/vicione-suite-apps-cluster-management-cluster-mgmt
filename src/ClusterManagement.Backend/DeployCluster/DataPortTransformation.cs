using ClusterManagement.Backend.Engines;
using ViciOne.Suite.DataPort;
using FunctionBlock = ViciOne.Cluster.Model.FunctionBlock;

namespace ClusterManagement.Backend.DeployCluster;

internal sealed class DataPortTransformation
{
    internal FunctionBlock FunctionBlock { get; init; } = new();
    internal string CommunicationId { get; set; } = string.Empty;
    internal IReadOnlyCollection<CommunicationProperty> CommunicationProperties { get; init; } = [];
    internal string? ConnectionName { get; init; }
    internal IReadOnlyCollection<Node> IncomingNodes { get; init; } = [];
    internal List<IncomingLink> IncomingLinks { get; init; } = [];
    internal IReadOnlyCollection<Node> OutgoingNodes { get; init; } = [];
    internal List<OutgoingLink> OutgoingLinks { get; init; } = [];
    internal List<FunctionBlockConnectorDesign> AdditionalConnectorDesigns { get; init; } = [];
}

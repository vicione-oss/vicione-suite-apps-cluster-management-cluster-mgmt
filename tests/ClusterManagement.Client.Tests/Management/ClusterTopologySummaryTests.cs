using ClusterManagement.Client.Management.Models;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Tests.Management;

public class ClusterTopologySummaryTests
{
    [Fact]
    public void Counts_only_the_nodes_the_deployment_deploys()
    {
        // Arrange
        var cluster = new Cluster
        {
            NodeGroups =
            [
                new ClusterNodeGroup
                {
                    Nodes = [new ClusterNode()],
                    NodeGroups = [new ClusterNodeGroup { Nodes = [new ClusterNode(), new ClusterNode()] }],
                },
            ],
        };

        // Act
        var summary = ClusterTopologySummary.Create(cluster);

        // Assert
        summary.Nodes.Should().Be(1);
    }

    [Fact]
    public void Counts_only_core_os_applications_as_instances()
    {
        // Arrange
        var node = new ClusterNode
        {
            Applications =
            [
                new ClusterApplication { Type = ClusterApplicationType.CoreOsStandalone },
                new ClusterApplication { Type = ClusterApplicationType.MessageBroker },
            ],
        };
        var cluster = new Cluster { NodeGroups = [new ClusterNodeGroup { Nodes = [node] }] };

        // Act
        var summary = ClusterTopologySummary.Create(cluster);

        // Assert
        summary.Instances.Should().Be(1);
    }

    [Fact]
    public void Counts_engine_hosts_and_their_engines()
    {
        // Arrange
        var application = new ClusterApplication
        {
            Type = ClusterApplicationType.CoreOsStandalone,
            EngineHosts =
            [
                new EngineHost { Engines = [new Engine(), new Engine()] },
                new EngineHost { Engines = [new Engine()] },
            ],
        };
        var cluster = new Cluster { NodeGroups = [new ClusterNodeGroup { Nodes = [new ClusterNode { Applications = [application] }] }] };

        // Act
        var summary = ClusterTopologySummary.Create(cluster);

        // Assert
        summary.EngineHosts.Should().Be(2);
        summary.Engines.Should().Be(3);
    }

    [Fact]
    public void Counts_function_blocks_of_nested_containers()
    {
        // Arrange
        var root = new Container
        {
            FunctionBlocks = [new FunctionBlock()],
            Containers = [new ChildContainer { FunctionBlocks = [new FunctionBlock(), new FunctionBlock()] }],
        };
        var cluster = new Cluster { Dataflows = [new Dataflow { Root = root }] };

        // Act
        var summary = ClusterTopologySummary.Create(cluster);

        // Assert
        summary.Dataflows.Should().Be(1);
        summary.FunctionBlocks.Should().Be(3);
    }

    [Fact]
    public void Counts_tree_nodes_with_a_value_type_as_datapoints()
    {
        // Arrange
        var folder = new DataPortTreeNode
        {
            Children = [new DataPortTreeNode { ValueType = typeof(int) }, new DataPortTreeNode { ValueType = typeof(float) }],
        };
        var dataPort = new DataPort { TreeNodes = [folder, new DataPortTreeNode { ValueType = typeof(bool) }] };
        var cluster = new Cluster { Dataflows = [new Dataflow { DataPorts = [dataPort] }] };

        // Act
        var summary = ClusterTopologySummary.Create(cluster);

        // Assert
        summary.DataPorts.Should().Be(1);
        summary.Datapoints.Should().Be(3);
    }

    [Fact]
    public void Counts_datapoints_linked_in_either_direction()
    {
        // Arrange
        var readByBlock = new DataPortTreeNode { Id = Guid.NewGuid(), ValueType = typeof(int) };
        var writtenByBlock = new DataPortTreeNode { Id = Guid.NewGuid(), ValueType = typeof(int), IncomingLinks = [new Link()] };
        var unlinked = new DataPortTreeNode { Id = Guid.NewGuid(), ValueType = typeof(int) };
        var block = new FunctionBlock { Inputs = [new ConnectorInput { IncomingLinks = [new Link { SourceDataPortTreeNodeId = readByBlock.Id }] }] };
        var cluster = new Cluster
        {
            Dataflows =
            [
                new Dataflow
                {
                    Root = new Container { FunctionBlocks = [block] },
                    DataPorts = [new DataPort { TreeNodes = [readByBlock, writtenByBlock, unlinked] }],
                },
            ],
        };

        // Act
        var summary = ClusterTopologySummary.Create(cluster);

        // Assert
        summary.Datapoints.Should().Be(3);
        summary.LinkedDatapoints.Should().Be(2);
    }
}

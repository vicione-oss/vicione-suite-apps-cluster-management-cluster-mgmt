using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Extensions;
using ClusterManagement.Public;
using ClusterManagement.Public.Designs;
using ClusterManagement.Tests.Tools.Factories;
using AwesomeAssertions;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;
using ViciOne.Core.Dataflow.DataModel;
using Xunit;

namespace ClusterManagement.Internal.Tests.Extensions;

public class ClusterBuilderExtensionsImportTests
{
    private const string ImportPropertyDesignId = "ImpProperty1";
    private const string ImportPropertyStringValue = "PropertyValue";

    private readonly FunctionBlockDesign[] _fbDesigns = ClusterFactory.CreateTestDesigns();

    [Fact]
    public void Import_same_cluster_again_wont_change_anything()
    {
        // Arrange
        using var builder = CreateTargetBuilder(_fbDesigns);
        using var import = CreateImportBuilder(_fbDesigns);

        // Act
        var firstResult = builder.ImportCluster(import.Cluster, new ClusterImportOptions());
        var secondResult = builder.ImportCluster(import.Cluster, new ClusterImportOptions());

        // Assert
        firstResult.TotalErrors.Should().BeEmpty();
        firstResult.TotalAdded.Should().BeGreaterThan(0);
        firstResult.TotalMerged.Should().Be(0);
        firstResult.TotalSkipped.Should().Be(3, "existing cluster dependencies Test|Mqtt + link");
        firstResult.Dependencies.Skipped.Should().Be(2);

        firstResult.TotalErrors.Should().BeEmpty();
        secondResult.TotalAdded.Should().Be(0);
        secondResult.TotalMerged.Should().Be(0);
        secondResult.TotalSkipped.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Import_with_merge_options_will_add_children_to_existing_items()
    {
        // Arrange
        using var builder = CreateTargetBuilder(_fbDesigns);
        using var import = CreateImportBuilder(_fbDesigns);
        var options = new ClusterImportOptions()
        {
            MergeNodeGroups = true,
            MergeNodes = true,
            MergeApplications = true,
            MergeEngineHosts = true,
        };

        // Act
        var result = builder.ImportCluster(import.Cluster, options);

        // Assert
        result.TotalErrors.Should().BeEmpty();
        result.TotalAdded.Should().BeGreaterThan(0);
        result.TotalMerged.Should().BeGreaterThan(0);
        result.TotalSkipped.Should().Be(3, "existing cluster dependencies Test|Mqtt + link");
        result.Dependencies.Skipped.Should().Be(2);

        result.NodeGroups.Merged.Should().Be(1);
        result.Nodes.Merged.Should().Be(1);
        result.Applications.Merged.Should().Be(1);
        result.EngineHosts.Merged.Should().Be(1);
    }

    [Fact]
    public void Import_with_default_options_will_only_add_not_existing_items()
    {
        // Arrange
        using var builder = CreateTargetBuilder(_fbDesigns);
        using var import = CreateImportBuilder(_fbDesigns);

        // Act
        var result = builder.ImportCluster(import.Cluster, new());

        // Assert
        result.TotalErrors.Should().BeEmpty();
        result.TotalAdded.Should().BeGreaterThan(0);
        result.TotalMerged.Should().Be(0);
        result.TotalSkipped.Should().Be(3, "existing cluster dependencies Test|Mqtt + link");
        result.TotalErrors.Should().BeEmpty();
        result.Dependencies.Skipped.Should().Be(2);

        builder.Cache.ClusterNodeGroups.Should().HaveCount(2);
        builder.Cache.ClusterNodes.Should().HaveCount(2);
        builder.Cache.ClusterApplications.Should().HaveCount(2);
        builder.Cache.EngineHosts.Should().HaveCount(2);
        builder.Cache.EngineGuids.Should().HaveCount(4);

        builder.Cache.DataPorts.Should().HaveCount(2);
        builder.Cache.DataPortTreeNodes.Should().HaveCount(7);
        builder.Cache.DataPortProperties.Should().HaveCount(4, "2 orig + 2 imported");
        builder.Cache.DataPortProperties.Should().Contain(k => k.DesignId == ImportPropertyDesignId && Equals(k.Value, ImportPropertyStringValue));

        builder.Cache.ContainerGuids.Should().HaveCount(5, "2*Root + imp/exp + exp.child");
        builder.Cache.FunctionBlockDesigns.Should().HaveCount(4);
        builder.Cache.FunctionBlocks.Should().HaveCount(4);
        builder.Cache.Labels.Should().HaveCount(2);
    }

    [Fact]
    public void Import_cluster_does_not_support_dependency_downgrade()
    {
        // Arrange
        using var builder = CreateTargetBuilder(_fbDesigns);
        using var import = CreateImportBuilder(_fbDesigns);

        var dependency = import.Cache.ClusterDependencies.First();
        import.Editors.Cluster.RemoveDependency(dependency);
        _ = import.Editors.Cluster.AddDependency(dependency.Name, new Version(dependency.Version.Major + 1, dependency.Version.Minor, dependency.Version.Build));

        // Act
        var result = builder.ImportCluster(import.Cluster, new ClusterImportOptions());

        // Assert
        result.Dependencies.Errors.Should().HaveCount(1);
    }

    [Fact]
    public void Import_cluster_does_support_dependency_upgrade()
    {
        // Arrange
        using var builder = CreateTargetBuilder(_fbDesigns);
        using var import = CreateImportBuilder(_fbDesigns);

        var dependency = builder.Cache.ClusterDependencies.First();
        builder.Editors.Cluster.RemoveDependency(dependency);
        _ = builder.Editors.Cluster.AddDependency(dependency.Name, new Version(dependency.Version.Major + 1, dependency.Version.Minor, dependency.Version.Build));

        // Act
        var result = builder.ImportCluster(import.Cluster, new ClusterImportOptions());

        // Assert
        result.Dependencies.Errors.Should().BeEmpty();
        result.Dependencies.Versions.Should().HaveCount(1);
        result.Dependencies.Versions.Should().Contain(k => k.PackageName == dependency.Name);
    }

    [Fact]
    public void Import_cluster_with_linked_dataport_connectors_should_add_links()
    {
        // Arrange
        using var builder = CreateTargetBuilder(_fbDesigns);
        using var import = CreateImportBuilder(_fbDesigns);

        var dependency = builder.Cache.ClusterDependencies.First();
        builder.Editors.Cluster.RemoveDependency(dependency);
        _ = builder.Editors.Cluster.AddDependency(dependency.Name, new Version(dependency.Version.Major + 1, dependency.Version.Minor, dependency.Version.Build));

        // Act
        var result = builder.ImportCluster(import.Cluster, new ClusterImportOptions());

        // Assert
        result.Links.Added.Should().Be(import.Cache.Links.Count);
    }

    private static ClusterBuilder CreateTargetBuilder(FunctionBlockDesign[] designs)
    {
        var functionBlockDesignId1 = designs[0];
        var functionBlockDesignId2 = designs[1];
        var dependencyDesigns = ClusterDependencyPackageFactory.CreateDependencyDesigns(designs);
        var resolver = new ClusterDependencyResolver(dependencyDesigns);
        var builder = new ClusterBuilder(resolver);

        // topology
        var nodeGroup = builder.Editors.Cluster.AddNodeGroup("TrgNodeGroup");
        var node = builder.Editors.NodeGroup.AddNode(nodeGroup, "TrgNode");
        var application = builder.Editors.Node.AddApplication(node, ClusterApplicationType.CoreOsStandalone);
        var engineHost = builder.Editors.Application.AddEngineHost(application, "TrgEngineHost");
        var engine = builder.Editors.EngineHost.AddEngine(engineHost, "TrgEngine");
        var dataflow = builder.Cluster.Dataflows.First();

        // container with 2 FunctionBlocks            
        var container = builder.Editors.Container.AddContainer(dataflow.Root, "TrgContainer", Guid.NewGuid());
        var functionBlock1 = builder.Editors.Container.AddFunctionBlock(
            container,
            functionBlockDesignId1.Id,
            "TrgFunctionBlock1",
            Guid.NewGuid());

        var functionBlock2 = builder.Editors.Container.AddFunctionBlock(
            container,
            functionBlockDesignId2.Id,
            "TrgFunctionBlock2",
            Guid.NewGuid());

        // Assign of the engine to the functionblock
        builder.Editors.FunctionBlock.AssignEngine(engine, functionBlock1, functionBlock2);
        builder.Editors.Connector.SetPublished(functionBlock1.ProcessDataOutputs.First(), true);

        var fb1Output = functionBlock1.Outputs.First();
        var fb2Input = functionBlock2.Inputs.Find(c => c.Value?.GetType() == fb1Output.Value?.GetType());
        _ = builder.Editors.Connector.AddLink(fb1Output, fb2Input!, true);

        builder.Editors.Container.AddLabel(dataflow.Root, new(100), new(new(100)), 200, "TrgLabel1");

        // DataPorts (MQTT)
        var dataPortEngine = builder.Editors.EngineHost.AddEngine(engineHost, "TrgDataPortEngine");
        var dataPort = builder.Editors.Dataflow.AddDataPort(dataflow, Constants.MqttDataPortDesignId, "TrgDataPort", DataPortDirection.InOut, Constants.MqttRuleSetId);

        builder.Editors.DataPort.AssignEngine(dataPortEngine, dataPort);
        builder.Editors.DataPort.AddProperty("TrgProperty1", dataPort, value: "TrgTest");
        builder.Editors.DataPort.AddProperty("TrgProperty2", dataPort, value: 23);

        // DataPortTreeNodes
        var srcConnectorType = builder.ResolveConnectorDesignSourceType(fb1Output);
        var dataPortRootNode = builder.Editors.DataPort.AddTreeNode(Constants.MqttDataPortTopicNodeId, dataPort, "TrgRootNode", null, DataPortTransferMode.None);
        var dataPortTopicNode = builder.Editors.DataPortTreeNode.AddTreeNode(Constants.MqttDataPortTopicNodeId, dataPortRootNode, "TrgTopicNode", null, DataPortTransferMode.None);
        var dataPortDataPointNode = builder.Editors.DataPortTreeNode.AddTreeNode(Constants.MqttDataPortTopicNodeId, dataPortTopicNode, "TrgDataPointNode", srcConnectorType, DataPortTransferMode.OnChange);
        _ = builder.Editors.DataPortTreeNode.AssignConnector(dataPortDataPointNode, fb1Output);

        return builder;
    }

    private static ClusterBuilder CreateImportBuilder(FunctionBlockDesign[] designs)
    {
        var functionBlockDesignId1 = designs[2];
        var functionBlockDesignId2 = designs[3];
        var dependencyDesigns = ClusterDependencyPackageFactory.CreateDependencyDesigns(designs);
        var resolver = new ClusterDependencyResolver(dependencyDesigns);
        var builder = new ClusterBuilder(resolver);

        // topology
        var nodeGroup = builder.Editors.Cluster.AddNodeGroup("ImpNodeGroup");
        var node = builder.Editors.NodeGroup.AddNode(nodeGroup, "ImpNode");
        var application = builder.Editors.Node.AddApplication(node, ClusterApplicationType.CoreOsStandalone);
        var engineHost = builder.Editors.Application.AddEngineHost(application, "ImpEngineHost");
        var engine = builder.Editors.EngineHost.AddEngine(engineHost, "ImpEngine");
        var dataflow = builder.Cluster.Dataflows.First();

        // container with 2 FunctionBlocks            
        var container1 = builder.Editors.Container.AddContainer(dataflow.Root, "ImpContainer", Guid.NewGuid());
        var functionBlock1 = builder.Editors.Container.AddFunctionBlock(
            container1,
            functionBlockDesignId1.Id,
            "ImpFunctionBlock1",
            Guid.NewGuid());

        var childContainer = builder.Editors.Container.AddContainer(container1, "ImpChildContainer", Guid.NewGuid());
        var functionBlock2 = builder.Editors.Container.AddFunctionBlock(
            childContainer,
            functionBlockDesignId2.Id,
            "ImpFunctionBlock2",
            Guid.NewGuid());

        // Assign of the engine to the functionblock
        builder.Editors.FunctionBlock.AssignEngine(engine, functionBlock1, functionBlock2);
        builder.Editors.Connector.SetPublished(functionBlock1.ProcessDataOutputs.First(), true);

        var fb1Output = functionBlock1.Outputs.First();
        var fb2Input = functionBlock2.Inputs.First(c => c.Value?.GetType() == fb1Output.Value?.GetType());

        _ = builder.Editors.Connector.AddLink(fb1Output, fb2Input, true);
        _ = builder.Editors.Connector.AddToParentContainer([.. functionBlock2.Outputs]);

        builder.Editors.Container.AddLabel(dataflow.Root, new(100), new(new(100)), 200, "ImpLabel1");

        // DataPorts (MQTT)
        var dataPortEngine = builder.Editors.EngineHost.AddEngine(engineHost, "ImpDataPortEngine");
        var dataPort = builder.Editors.Dataflow.AddDataPort(dataflow, Constants.MqttDataPortDesignId, "ImpDataPort", DataPortDirection.InOut, Constants.MqttRuleSetId);
        builder.Editors.DataPort.AssignEngine(dataPortEngine, dataPort);
        builder.Editors.DataPort.AddProperty(ImportPropertyDesignId, dataPort, value: ImportPropertyStringValue);
        builder.Editors.DataPort.AddProperty("ImpProperty2", dataPort, value: 42);

        // link from fb1Output connector to dataport tree node
        var srcConnectorType = builder.ResolveConnectorDesignSourceType(fb1Output);
        var dataPortRootNode = builder.Editors.DataPort.AddTreeNode(Constants.MqttDataPortTopicNodeId, dataPort, "ImpRootNode", null, DataPortTransferMode.None);
        var dataPortTopicNode = builder.Editors.DataPortTreeNode.AddTreeNode(Constants.MqttDataPortTopicNodeId, dataPortRootNode, "ImpTopicNode", null, DataPortTransferMode.None);
        var dataPortDataPointNode = builder.Editors.DataPortTreeNode.AddTreeNode(Constants.MqttDataPortTopicNodeId, dataPortTopicNode, "ImpDataPointNode", srcConnectorType, DataPortTransferMode.OnChange);
        _ = builder.Editors.DataPortTreeNode.AssignConnector(dataPortDataPointNode, fb1Output);

        // link from container output to dataport (fb2 outs are added parent)            
        var containerOutput = functionBlock2.Outputs.First();
        var containerOutputType = builder.ResolveConnectorDesignSourceType(containerOutput);
        var containerOutputNode = builder.Editors.DataPortTreeNode.AddTreeNode(Constants.MqttDataPortTopicNodeId, dataPortTopicNode, "ImpContainerPointNode", containerOutputType, DataPortTransferMode.OnChange);
        _ = builder.Editors.DataPortTreeNode.AssignConnector(containerOutputNode, containerOutput);

        return builder;
    }
}

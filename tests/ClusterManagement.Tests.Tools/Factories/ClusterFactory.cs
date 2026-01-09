using ClusterManagement.Internal.Extensions;
using ClusterManagement.Public;
using ClusterManagement.Public.Designs;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;
using ViciOne.Core.Dataflow.DataModel;

namespace ClusterManagement.Tests.Tools.Factories;

public enum ClusterComplexity
{
    Simple,

    /// <summary>
    /// event enabled: 1 out-/input connector
    /// todo: add more desctiption
    /// </summary>
    Complex,

    Ultra
}

public static class ClusterFactory
{
    public const string LabelContentC40 = "Lorem ipsum dolor sit amet, consetetur s";

    public static class ClusterNodeGroups
    {
        public const string ClusterNodeGroup1 = "ClusterNodeGroup1";

        public static readonly Guid ClusterNodeGroupId1 = Guid.NewGuid();
    }

    public static class ClusterNodes
    {
        public const string ClusterNode1 = "ClusterNode1";
        public const string ClusterNode2 = "ClusterNode2";
        public const string ClusterNode3 = "ClusterNode3";

        public static readonly Guid ClusterNodeId1 = Guid.NewGuid();
        public static readonly Guid ClusterNodeId2 = Guid.NewGuid();
        public static readonly Guid ClusterNodeId3 = Guid.NewGuid();
    }

    public static class EngineHosts
    {
        public const string EngineHost1 = "EngineHost1";
        public const string EngineHost2 = "EngineHost2";
        public const string EngineHost3 = "EngineHost3";

        public static readonly Guid EngineHostId1 = Guid.NewGuid();
        public static readonly Guid EngineHostId2 = Guid.NewGuid();
        public static readonly Guid EngineHostId3 = Guid.NewGuid();
    }

    public static class Engines
    {
        public const string Engine1 = "Engine1";
        public const string Engine2 = "Engine2";
        public const string Engine3 = "Engine3";

        public static readonly Guid EngineId1 = Guid.NewGuid();
        public static readonly Guid EngineId2 = Guid.NewGuid();
        public static readonly Guid EngineId3 = Guid.NewGuid();
    }

    public static class DataPorts
    {
        public const string DataPort1 = "DataPort1";
        public const string DataPort2 = "DataPort2";
        public const string DataPort3 = "DataPort3";

        public static readonly Guid DataPortDesign1Id = Guid.NewGuid();
        public static readonly Guid DataPortDesign2Id = Guid.NewGuid();

        public static readonly Guid DataPortId1 = Guid.NewGuid();
        public static readonly Guid DataPortId2 = Guid.NewGuid();
        public static readonly Guid DataPortId3 = Guid.NewGuid();
    }

    public static class DataPortTreeNodes
    {
        public const string DataPortTreeNode1 = "DataPortTreeNode1";
        public const string DataPortTreeNode2 = "DataPortTreeNode2";
        public const string DataPortTreeNode3 = "DataPortTreeNode3";

        public static readonly Guid DataPortTreeNodeId1 = Guid.NewGuid();
        public static readonly Guid DataPortTreeNodeId2 = Guid.NewGuid();
        public static readonly Guid DataPortTreeNodeId3 = Guid.NewGuid();
    }

    public static class Containers

    {
        public const string Container = "Container";
        public const string ChildContainer1 = "ChildContainer1";
        public const string ChildContainer2 = "ChildContainer2";
        public const string ChildContainer3 = "ChildContainer3";

        public static readonly Guid ContainerId = Guid.NewGuid();
        public static readonly Guid ChildContainer1Id = Guid.NewGuid();
        public static readonly Guid ChildContainer2Id = Guid.NewGuid();
        public static readonly Guid ChildContainer3Id = Guid.NewGuid();
    }

    public static class FunctionBlocks
    {
        public const string FunctionBlock1CC1 = "FunctionBlock1CC1";
        public const string FunctionBlock2CC1 = "FunctionBlock2CC1";
        public const string FunctionBlock3CC1 = "FunctionBlock3CC1";
        public const string FunctionBlock1CC2 = "FunctionBlock1CC2";
        public const string FunctionBlock1CC3 = "FunctionBlock1CC3";

        public static readonly Guid FunctionBlock1CC1Id = Guid.NewGuid();
        public static readonly Guid FunctionBlock2CC1Id = Guid.NewGuid();
        public static readonly Guid FunctionBlock3CC1Id = Guid.NewGuid();
        public static readonly Guid FunctionBlock1CC2Id = Guid.NewGuid();
        public static readonly Guid FunctionBlock1CC3Id = Guid.NewGuid();
    }

    public static class Links
    {
        public static readonly Guid LinkId1 = Guid.NewGuid();
        public static readonly Guid LinkId2 = Guid.NewGuid();
    }

    public static class Labels
    {
        public const string Label1 = "Label1";
        public const string Label2 = "Label2";
        public const string Label3 = "Label3";

        public const string LabelContent1 = $"{LabelContentC40}***{Label1}";
        public const string LabelContent2 = $"{LabelContentC40}***{Label2}";
        public const string LabelContent3 = $"{LabelContentC40}***{Label3}";

        public static readonly Guid LabelId1 = Guid.NewGuid();
        public static readonly Guid LabelId2 = Guid.NewGuid();
        public static readonly Guid LabelId3 = Guid.NewGuid();
    }

    public static FunctionBlockDesign[] CreateTestDesigns() => [
            FunctionBlockDesignFactory.Create1I1O1("Design1"),
            FunctionBlockDesignFactory.Create2I4O1("Design2"),
            FunctionBlockDesignFactory.Create3I3O2("Design3"),
            FunctionBlockDesignFactory.Create4I2O2("Design4"),
            FunctionBlockDesignFactory.CreateI3O2("DesignI3O2"),
            ];

    public static ClusterBuilder CreateComplexCluster(ClusterComplexity complexity = ClusterComplexity.Simple, FunctionBlockDesign[]? designs = null)
    {
        var functionBlockDesigns = designs ?? CreateTestDesigns();
        var dependencyDesigns = ClusterDependencyPackageFactory.CreateDependencyDesigns(functionBlockDesigns);

        var functionBlockDesign1I1O1 = functionBlockDesigns[0];
        var functionBlockDesign2I4O1 = functionBlockDesigns[1];
        var functionBlockDesign3I1O2 = functionBlockDesigns[2];
        var functionBlockDesign4I2O2 = functionBlockDesigns[3];
        var functionBlockDesignI3O2 = functionBlockDesigns[4];

        var resolver = new ClusterDependencyResolver(dependencyDesigns);
        var builder = new ClusterBuilder(resolver);
        var dataflow = builder.Cluster.Dataflows.First();

        // ******************** Cluster ***********************
        var clusterNodeGroup = builder.Editors.Cluster.AddNodeGroup(ClusterNodeGroups.ClusterNodeGroup1, ClusterNodeGroups.ClusterNodeGroupId1);

        //      Create DataflowTreeNode (TBD)

        //      Create 2 ClusterNodes
        var clusterNode1 = builder.Editors.NodeGroup.AddNode(clusterNodeGroup, ClusterNodes.ClusterNode1, ClusterNodes.ClusterNodeId1);
        var clusterNode2 = builder.Editors.NodeGroup.AddNode(clusterNodeGroup, ClusterNodes.ClusterNode2, ClusterNodes.ClusterNodeId2);

        //          Create 1 ClusterApplication
        var clusterApplication1 = builder.Editors.Node.AddApplication(clusterNode1, ClusterApplicationType.CoreOsStandalone);
        var clusterApplication2 = builder.Editors.Node.AddApplication(clusterNode2, ClusterApplicationType.CoreOsStandalone);

        //              Create 2 EngineHosts
        var engineHost1 = builder.Editors.Application.AddEngineHost(clusterApplication1, EngineHosts.EngineHost1, EngineHosts.EngineHostId1);
        var engineHost2 = builder.Editors.Application.AddEngineHost(clusterApplication2, EngineHosts.EngineHost2, EngineHosts.EngineHostId2);

        //                  Create 3 Engines
        var engine1 = builder.Editors.EngineHost.AddEngine(engineHost1, Engines.Engine1, Engines.EngineId1);
        var engine2 = builder.Editors.EngineHost.AddEngine(engineHost1, Engines.Engine2, Engines.EngineId2);
        var engine3 = builder.Editors.EngineHost.AddEngine(engineHost1, Engines.Engine3, Engines.EngineId3);

        // ******************** Dataflow ***********************
        // Create a first ChildContainer (parent: dataflow)
        var childContainer1Eb1 = builder.Editors.Container.AddContainer(dataflow.Root, Containers.ChildContainer1, Containers.ChildContainer1Id);

        //  Create 2 FunctionBlocks
        var functionBlock1CC1 = builder.Editors.Container.AddFunctionBlock(
            childContainer1Eb1,
            functionBlockDesign1I1O1.Id,
            FunctionBlocks.FunctionBlock1CC1,
            FunctionBlocks.FunctionBlock1CC1Id);
        var functionBlock2CC1 = builder.Editors.Container.AddFunctionBlock(
            childContainer1Eb1,
            functionBlockDesign2I4O1.Id,
            FunctionBlocks.FunctionBlock2CC1,
            FunctionBlocks.FunctionBlock2CC1Id);

        if (complexity == ClusterComplexity.Simple)
            return builder;

        // Create ContainerConnectors for ChildContainer1
        var connectorsCC1 = new IConnector[2];
        connectorsCC1[0] = functionBlock1CC1.ProcessDataInputs.First();
        connectorsCC1[1] = functionBlock2CC1.ProcessDataOutputs.Last();
        var containerConnectorCC1 = builder.Editors.Connector.AddToParentContainer(connectorsCC1).ToArray();

        // event enable 2 connectors
        var connectorsEventEnabled = new IConnector[2];
        connectorsEventEnabled[0] = functionBlock2CC1.ProcessDataInputs.First();
        connectorsEventEnabled[1] = functionBlock2CC1.ProcessDataOutputs.First();
        builder.Editors.Connector.SetEventEnabled(true, connectorsEventEnabled);

        // Create a second ChildContainer (parent: dataflow)
        var childContainer2Eb1 = builder.Editors.Container.AddContainer(dataflow.Root, Containers.ChildContainer2, Containers.ChildContainer2Id);

        //  Create 1 FunctionBlock
        var functionBlock1CC2 = builder.Editors.Container.AddFunctionBlock(
            childContainer2Eb1,
            functionBlockDesign3I1O2.Id,
            FunctionBlocks.FunctionBlock1CC2,
            FunctionBlocks.FunctionBlock1CC2Id);

        // Create a third ChildContainer (parent: childContainer2)
        var childContainer3Eb2 = builder.Editors.Container.AddContainer(childContainer2Eb1, Containers.ChildContainer3, Containers.ChildContainer3Id);

        //  Create 1 FunctionBlock
        var functionBlock1CC3 = builder.Editors.Container.AddFunctionBlock(
            childContainer3Eb2,
            functionBlockDesign4I2O2.Id,
            FunctionBlocks.FunctionBlock1CC3,
            FunctionBlocks.FunctionBlock1CC3Id);

        // Publish Connector from ChildContainer1
        var publishFB1Connector = functionBlock1CC1.ProcessDataInputs.First();
        builder.Editors.Connector.SetPublished(publishFB1Connector, true);

        // Create ContainerConnectors for ChildContainer2 and ChildContainer3
        var connectorsCC3 = new IConnector[2];
        connectorsCC3[0] = functionBlock1CC3.ProcessDataInputs.First();
        connectorsCC3[1] = functionBlock1CC3.ProcessDataOutputs.Last();
        var containerConnectorsCC3 = builder.Editors.Connector.AddToParentContainer(connectorsCC3).ToArray();
        var containerConnectorsCC2 =
            builder.Editors.Connector.AddToParentContainer(containerConnectorsCC3).ToArray();

        if (complexity == ClusterComplexity.Complex)
            return builder;

        // Publish Connector from ChildContainer2
        var publishCC2Connector = containerConnectorsCC2[0].Connector;
        builder.Editors.Connector.SetPublished(publishCC2Connector, true);

        // Assign of the engine to the functionblock
        builder.Editors.FunctionBlock.AssignEngine(engine1, functionBlock1CC1, functionBlock2CC1);
        builder.Editors.FunctionBlock.AssignEngine(engine2, functionBlock1CC2, functionBlock1CC3);

        // Create 3 Labels
        builder.Editors.Container.AddLabel(dataflow.Root, new(100), new(new(100)), 200, Labels.LabelContent1, Labels.LabelId1);
        builder.Editors.Container.AddLabel(dataflow.Root, new(100), new(new(100)), 200, Labels.LabelContent2, Labels.LabelId2);
        builder.Editors.Container.AddLabel(dataflow.Root, new(100), new(new(100)), 200, Labels.LabelContent3, Labels.LabelId3);

        // Create Link
        var sourceFB1ConnOut2 = functionBlock1CC1.Outputs.Find(c => c.DesignId == FunctionBlockDesignFactory.Connectors.ConnectorFB1O1Id);
        var destFB2ConnIn3 = functionBlock2CC1.Inputs.Find(c => c.DesignId == FunctionBlockDesignFactory.Connectors.ConnectorFB2I3Id);
        var link1 = builder.Editors.Connector.AddLink(sourceFB1ConnOut2!, destFB2ConnIn3!, true, Links.LinkId1);

        // Create Link from FB2-ConnectorOutput to DataPortTreeNode
        var sourceFB2ConnOut = functionBlock2CC1.Outputs.Find(c => c.DesignId == FunctionBlockDesignFactory.Connectors.ConnectorFB2O1Id);

        //                      Create 2 DataPorts
        var dataPort1 = builder.Editors.Dataflow.AddDataPort(dataflow, Constants.MqttDataPortDesignId, DataPorts.DataPort1, DataPortDirection.InOut, Constants.MqttRuleSetId, DataPorts.DataPortId1);
        var dataPort2 = builder.Editors.Dataflow.AddDataPort(dataflow, Constants.MqttDataPortDesignId, DataPorts.DataPort2, DataPortDirection.InOut, Constants.MqttRuleSetId, DataPorts.DataPortId2);

        // use connector design to get type to be linked with dataport node
        var connectorType = builder.ResolveConnectorDesignSourceType(sourceFB2ConnOut);

        //                          Create 3 DataPortTreeNodes
        var dataPortTreeNode1 = builder.Editors.DataPort.AddTreeNode(Constants.MqttDataPortTopicNodeId, dataPort1, DataPortTreeNodes.DataPortTreeNode1, null, DataPortTransferMode.OnChange, DataPortTreeNodes.DataPortTreeNodeId1);
        var dataPortTreeNode2 = builder.Editors.DataPort.AddTreeNode(Constants.MqttDataPortTopicNodeId, dataPort1, DataPortTreeNodes.DataPortTreeNode2, connectorType, DataPortTransferMode.OnChange, DataPortTreeNodes.DataPortTreeNodeId2);
        var dataPortTreeNode3 = builder.Editors.DataPort.AddTreeNode(Constants.MqttDataPortTopicNodeId, dataPort1, DataPortTreeNodes.DataPortTreeNode3, null, DataPortTransferMode.OnChange, DataPortTreeNodes.DataPortTreeNodeId3);

        //  create a link between the connector and the dataport tree node
        var link4 = builder.Editors.DataPortTreeNode.AssignConnector(dataPortTreeNode2, sourceFB2ConnOut!);


        // Check for hierarchy dataflow behavior
        var depthContainer1 = builder.Editors.Container.AddContainer(dataflow.Root, "DepthContainer1");
        var depthContainer2 = builder.Editors.Container.AddContainer(depthContainer1, "DepthContainer2");
        var depthContainer3 = builder.Editors.Container.AddContainer(depthContainer2, "DepthContainer3");
        var depthContainer4 = builder.Editors.Container.AddContainer(depthContainer3, "DepthContainer4");
        var depthContainer5 = builder.Editors.Container.AddContainer(depthContainer4, "DepthContainer5");

        //  Create 2 FunctionBlocks
        var depthFb1 = builder.Editors.Container.AddFunctionBlock(
            depthContainer5,
            functionBlockDesign1I1O1.Id,
            "DepthFb1");
        var depthFb2 = builder.Editors.Container.AddFunctionBlock(
            depthContainer5,
            functionBlockDesign2I4O1.Id,
            "DepthFb2");

        // event enable 2 connectors
        var depthsConnectorsEventEnabled = new IConnector[2];
        connectorsEventEnabled[0] = depthFb1.ProcessDataInputs.First();
        connectorsEventEnabled[1] = depthFb2.ProcessDataOutputs.First();
        builder.Editors.Connector.SetEventEnabled(true, connectorsEventEnabled);

        return builder;
    }

    public static ClusterBuilder CreateCopy(ClusterBuilder builder, Version? newVersion = null)
    {
        // we have to de-/serialize it to create a new cluster object reference
        var clusterJson = ClusterSerializer.Serialize(builder.Cluster);
        var clusterConvert = ClusterSerializer.Deserialize(clusterJson);

        if (newVersion is not null)
            clusterConvert.Version = newVersion;

        return new(clusterConvert, builder.DependencyResolver);
    }
}

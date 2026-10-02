using AwesomeAssertions.Extensions;
using ClusterManagement.Backend.DeployCluster;
using ClusterManagement.Backend.Engines;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Internal.Extensions;
using ClusterManagement.Public;
using ClusterManagement.Public.Designs;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Extensions;
using ClusterManagement.Tests.Tools.Factories;
using Sdk.Connections.Contracts;
using ViciOne.Cluster.Builder;
using ViciOne.Tree.Builder;
using ViciOne.Cluster.Model;
using ViciOne.Core.Contracts.DataModel;
using ViciOne.Engine.DefaultPoolings;
using ViciOne.Suite.DataPort;
using ViciOne.Suite.System.Contracts;
using Connector = ViciOne.Cluster.Model.Connector;
using DataPort = ViciOne.Cluster.Model.DataPort;
using DataPortTransferMode = ViciOne.Cluster.Model.DataPortTransferMode;
using Engine = ViciOne.Cluster.Model.Engine;
using FunctionBlock = ViciOne.Cluster.Model.FunctionBlock;
using Link = ViciOne.Cluster.Model.Link;

namespace ClusterManagement.Backend.Tests.DeployCluster;

public class DataPortTransformerFacts
{
    public sealed class TransformToFunctionBlocks : IDisposable
    {
        private readonly ClusterBuilder _builder;
        private readonly ISuiteRulesetProvider _rulesetProvider;
        private readonly IReadOnlyCollection<EngineHost> _engineHosts;
        private readonly Engine _engine;
        private readonly DataPort _dataPort;

        public TransformToFunctionBlocks()
        {
            var dataPortFunctionBlockDesign = FunctionBlockDesignBuilder.CreateDataPortFunctionBlockDesign();

            _builder = new(new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([dataPortFunctionBlockDesign,])));
            _builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(dataPortFunctionBlockDesign.Id);

            _rulesetProvider = Substitute.For<ISuiteRulesetProvider>();
            _rulesetProvider.GetRuleset(Arg.Any<string>()).Returns(RulesetFactory.CreateTestRulesetMqtt());

            var engineHost = _builder.CreateTopologyToEngineHost();
            _engineHosts = [engineHost,];
            _engine = _builder.Editors.EngineHost.AddEngine(engineHost, "engine");
            var dataflow = _builder.Cluster.Dataflows.First();
            _dataPort = _builder.Editors.Dataflow.AddDataPort(dataflow, Constants.MqttDataPortDesignId, "dataPort", DataPortDirection.InOut, Constants.MqttRuleSetId);
            _builder.Editors.DataPort.AssignEngine(_engine, _dataPort);
        }

        public void Dispose() => _builder.Dispose();

        [Fact]
        public void Ignores_DataPort_without_assigned_engine()
        {
            _builder.Editors.DataPort.UnassignEngine(_dataPort);

            var transformation = DataPortTransformer.TransformToFunctionBlocks(_builder, _engineHosts, _rulesetProvider);

            transformation.Should().BeEmpty();
        }

        [Fact]
        public void Assigns_engine_of_DataPort_to_generated_FunctionBlock()
        {
            var transformation = DataPortTransformer.TransformToFunctionBlocks(_builder, _engineHosts, _rulesetProvider);

            transformation.Should().ContainKey(_engine).WhoseValue.Should().ContainSingle().Which.FunctionBlock.Engine.Should().BeSameAs(_engine);
        }
    }

    public sealed class TransformToFunctionBlock : IDisposable
    {
        private readonly ClusterBuilder _builder;
        private readonly ISuiteRulesetProvider _rulesetProvider;

        private readonly ConnectorInput _destinationConnector;
        private readonly ConnectorOutput _sourceConnector;
        private readonly Type _valueType;

        public TransformToFunctionBlock()
        {
            var functionBlockDesign = FunctionBlockDesignBuilder.Create(name: "OtherFB");
            functionBlockDesign.AddInput<int>();
            functionBlockDesign.AddOutput<int>();
            _valueType = typeof(int);

            var dataPortFunctionBlockDesign = FunctionBlockDesignBuilder.CreateDataPortFunctionBlockDesign();

            _builder = new(new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([dataPortFunctionBlockDesign, functionBlockDesign,])));
            _builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(dataPortFunctionBlockDesign.Id);
            _builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(functionBlockDesign.Id);

            _rulesetProvider = Substitute.For<ISuiteRulesetProvider>();
            _rulesetProvider.GetRuleset(Arg.Any<string>()).Returns(RulesetFactory.CreateTestRulesetMqtt());

            var functionBlock = _builder.Editors.Container.AddFunctionBlock(_builder.Cluster.Dataflows.First().Root, functionBlockDesign.Id);
            _sourceConnector = functionBlock.ProcessDataOutputs.First();
            _destinationConnector = functionBlock.ProcessDataInputs.First();
        }

        public void Dispose() => _builder.Dispose();

        private DataPort CreateDataPort(DataPortDirection dataPortDirection = DataPortDirection.InOut)
        {
            var engine = _builder.CreateTopologyToEngine("engine");
            var dataflow = _builder.Cluster.Dataflows.First();
            var dataPort = _builder.Editors.Dataflow.AddDataPort(dataflow, Constants.MqttDataPortDesignId, "dataPort", dataPortDirection, Constants.MqttRuleSetId);
            _builder.Editors.DataPort.AssignEngine(engine, dataPort);
            return dataPort;
        }

        [Fact]
        public void Adds_FunctionBlock_to_container_of_DataPort()
        {
            var dataflow1 = _builder.Editors.Cluster.AddDataflow("dataflow1", new Version());
            var dataflow2 = _builder.Editors.Cluster.AddDataflow("dataflow2", new Version());
            var dataPort1 = _builder.Editors.Dataflow.AddDataPort(dataflow1, Constants.MqttDataPortDesignId, "dataPort", DataPortDirection.Out, Constants.MqttRuleSetId);
            var dataPort2 = _builder.Editors.Dataflow.AddDataPort(dataflow2, Constants.MqttDataPortDesignId, "dataPort", DataPortDirection.Out, Constants.MqttRuleSetId);
            var engine1 = _builder.CreateTopologyToEngine("engineA");
            var engine2 = _builder.CreateTopologyToEngine("engineB");
            _builder.Editors.DataPort.AssignEngine(engine1, dataPort1);
            _builder.Editors.DataPort.AssignEngine(engine2, dataPort2);

            var transformation1 = DataPortTransformer.TransformToFunctionBlock(dataPort1, _builder, _rulesetProvider);
            var transformation2 = DataPortTransformer.TransformToFunctionBlock(dataPort2, _builder, _rulesetProvider);

            transformation1.FunctionBlock.Container.Should().BeSameAs(dataPort1.Dataflow.Root);
            transformation2.FunctionBlock.Container.Should().BeSameAs(dataPort2.Dataflow.Root);
        }

        [Fact]
        public void Adds_description_of_DataPort_to_FunctionBlock()
        {
            var dataPort = CreateDataPort();
            _builder.Editors.DataPort.SetDescription(dataPort, "exchange data with external system");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.Description.Should().Be(dataPort.Description);
        }

        [Fact]
        public void Transfers_DataPort_properties_to_external_communication_properties()
        {
            var dataPort = CreateDataPort();
            dataPort.Properties.Add(new() { DesignId = "Byte", Value = (byte)0, });
            dataPort.Properties.Add(new() { DesignId = "String", Value = "host", });
            dataPort.Properties.Add(new() { DesignId = "UInt16", Value = (ushort)1884, });
            dataPort.Properties.Add(new() { DesignId = "Enum", Value = MqttConnectionType.TCP, });

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.CommunicationId.Should().Be("MqttDataPort");
            transformation.CommunicationProperties.Should().BeEquivalentTo(new CommunicationProperty[]
            {
                new("Byte") { Value = TypedObject.CreateMandatory((byte)0), },
                new("String") { Value = TypedObject.CreateMandatory("host"), },
                new("UInt16") { Value = TypedObject.CreateMandatory((ushort)1884), },
                new("Enum") { Value = TypedObject.CreateMandatory(MqttConnectionType.TCP), },
                new("Name") { Value = TypedObject.CreateMandatory("dataPort"), },
                new("DesignId") { Value = TypedObject.CreateMandatory("MqttClientInstance"), },
            });
        }

        [Fact]
        public void Throws_if_the_ruleset_declares_a_node_type_twice()
        {
            var rulesetProvider = Substitute.For<ISuiteRulesetProvider>();
            rulesetProvider.GetRuleset(Arg.Any<string>()).Returns(RulesetFactory.CreateTestRuleset("""
                Common: { Target: DataPort, ContentType: Rules, Version: 1.0, Date: 2026-09-02, Description: Test, Author: Test, MetadataType: YAML }
                Root: { Id: MqttDataPort, Name: MQTT, Description: Test, ChildNodes: [ { Id: Folder } ] }
                Nodes:
                  - { Id: Folder, MappingId: Topic, Type: DataPortTreeNode, Name: Folder, Description: Test, TransferModes: [ None ] }
                  - { Id: Folder, MappingId: Topic, Type: DataPortTreeNode, Name: Folder, Description: Test, TransferModes: [ None ] }
                """));
            var dataPort = CreateDataPort();

            var act = FluentActions.Invoking(() => DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, rulesetProvider));

            act.Should().Throw<RulesetValidationFailedException>()
                .Which.Messages.Should().Contain(m => m.Contains("Folder", StringComparison.Ordinal));
        }

        [Fact]
        public void Ignores_dataport_without_nodes()
        {
            var dataPort = CreateDataPort();

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            ShouldBeEmpty(transformation);
        }

        [Fact]
        public void Ignores_nodes_without_transfer_mode()
        {
            var dataPort = CreateDataPort();
            var group = AddDataPortTreeNode(dataPort, "Group");
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", parent: group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _destinationConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            ShouldBeEmpty(transformation);
        }

        [Fact]
        public void Throws_if_node_has_unsupported_transfer_mode()
        {
            var dataPort = CreateDataPort();
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", transferMode: (DataPortTransferMode)(-28), valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);

            var act = FluentActions.Invoking(() => DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider));

            act.Should().Throw<NotSupportedException>().WithMessage($"*{leaf.Id}*{leaf.Name}*unsupported*transfer mode*-28*");
        }

        [Fact]
        public void Throws_if_a_linked_node_transfers_without_a_value_type()
        {
            var dataPort = CreateDataPort();
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", transferMode: DataPortTransferMode.OnChange, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);
            // The editor refuses to link a node that has no value type, so a cluster only reaches
            // deployment in this state through an import or an older editor.
            leaf.ValueType = null;

            var act = FluentActions.Invoking(() => DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider));

            act.Should().Throw<InvalidOperationException>().WithMessage($"*{leaf.Id}*{leaf.Name}*no value data type*");
        }

        [Fact]
        public void Ignores_nodes_without_links()
        {
            var dataPort = CreateDataPort();
            var group = AddDataPortTreeNode(dataPort, "Group");
            AddDataPortTreeNode(dataPort, "Leaf", transferMode: DataPortTransferMode.OnChange, parent: group);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            ShouldBeEmpty(transformation);
        }

        [Fact]
        public void Leaf_node_with_incoming_link_and_direction_In()
        {
            var dataPort = CreateDataPort(DataPortDirection.In);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set");
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.OnChange,
                transferPooling: new AveragePooling(), parent: group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(leaf, _sourceConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*in*");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            ShouldBeEmpty(transformation);
        }

        [Fact]
        public void Leaf_node_with_incoming_link_and_direction_InOut()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set");
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.OnChange,
                transferPooling: new AveragePooling(), parent: group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            var input = transformation.FunctionBlock.ProcessDataInputs.Should().ContainSingle().Subject;
            ShouldRepresentNode(input, leaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, input);

            var output = transformation.FunctionBlock.ProcessDataOutputs.Should().ContainSingle().Subject;
            ShouldRepresentNode(output, leaf, DataPortDirection.Out);
            ShouldHaveNoLinks(output);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    nodeLink => ShouldRepresentNodeLink(nodeLink, input, output)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    outputPooling => ShouldRepresentOutputConfiguration<AveragePooling>(outputPooling, output)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    transferMode => ShouldRepresentTransferMode<OnChangedTransferMode>(transferMode, output)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                inputDesign => ShouldExtendConnector(inputDesign, input, leaf),
                outputDesign => ShouldExtendConnector(outputDesign, output, leaf));

            transformation.IncomingLinks.Should().BeEmpty();
            transformation.OutgoingLinks.Should().SatisfyRespectively(
                link => ShouldRepresentExternalLink(link, output));

            transformation.IncomingNodes.Should().BeEmpty();
            transformation.OutgoingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [output,], [output,], _valueType));
        }

        [Fact]
        public void Leaf_node_with_incoming_link_and_direction_Out()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set");
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.OnChange,
                transferPooling: new AveragePooling(), parent: group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            var input = transformation.FunctionBlock.ProcessDataInputs.Should().ContainSingle().Subject;
            ShouldRepresentNode(input, leaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, input);

            var output = transformation.FunctionBlock.ProcessDataOutputs.Should().ContainSingle().Subject;
            ShouldRepresentNode(output, leaf, DataPortDirection.Out);
            ShouldHaveNoLinks(output);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    nodeLink => ShouldRepresentNodeLink(nodeLink, input, output)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    outputPooling => ShouldRepresentOutputConfiguration<AveragePooling>(outputPooling, output)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    transferMode => ShouldRepresentTransferMode<OnChangedTransferMode>(transferMode, output)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                inputDesign => ShouldExtendConnector(inputDesign, input, leaf),
                outputDesign => ShouldExtendConnector(outputDesign, output, leaf));

            transformation.IncomingLinks.Should().BeEmpty();
            transformation.OutgoingLinks.Should().SatisfyRespectively(
                link => ShouldRepresentExternalLink(link, output));

            transformation.IncomingNodes.Should().BeEmpty();
            transformation.OutgoingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [output,], [output,], _valueType));
        }

        [Fact]
        public void Leaf_node_with_outgoing_link_and_direction_In()
        {
            var dataPort = CreateDataPort(DataPortDirection.In);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set");
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.Periodic, 100,
                new AveragePooling(), group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _destinationConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            var input = transformation.FunctionBlock.ProcessDataInputs.Should().ContainSingle().Subject;
            ShouldRepresentNode(input, leaf, DataPortDirection.In);
            ShouldHaveNoLinks(input);

            var output = transformation.FunctionBlock.ProcessDataOutputs.Should().ContainSingle().Subject;
            ShouldRepresentNode(output, leaf, DataPortDirection.In);
            ShouldHaveLinkTo(output, _destinationConnector);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    nodeLink => ShouldRepresentNodeLink(nodeLink, input, output)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    outputPooling => ShouldRepresentOutputConfiguration<AveragePooling>(outputPooling, output)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    transferMode => ShouldRepresentTransferMode(transferMode, output, 100.Milliseconds())));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                inputDesign => ShouldExtendConnector(inputDesign, input, leaf),
                outputDesign => ShouldExtendConnector(outputDesign, output, leaf));

            transformation.IncomingLinks.Should().SatisfyRespectively(
                link => ShouldRepresentExternalLink(link, leaf, input));
            transformation.OutgoingLinks.Should().BeEmpty();

            transformation.IncomingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [input,], [input,], _valueType));
            transformation.OutgoingNodes.Should().BeEmpty();
        }

        [Fact]
        public void Leaf_node_with_outgoing_link_and_direction_InOut()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set");
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.Periodic, 100,
                new AveragePooling(), group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _destinationConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            var input = transformation.FunctionBlock.ProcessDataInputs.Should().ContainSingle().Subject;
            ShouldRepresentNode(input, leaf, DataPortDirection.In);
            ShouldHaveNoLinks(input);

            var output = transformation.FunctionBlock.ProcessDataOutputs.Should().ContainSingle().Subject;
            ShouldRepresentNode(output, leaf, DataPortDirection.In);
            ShouldHaveLinkTo(output, _destinationConnector);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    nodeLink => ShouldRepresentNodeLink(nodeLink, input, output)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    outputPooling => ShouldRepresentOutputConfiguration<AveragePooling>(outputPooling, output)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    transferMode => ShouldRepresentTransferMode(transferMode, output, 100.Milliseconds())));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                inputDesign => ShouldExtendConnector(inputDesign, input, leaf),
                outputDesign => ShouldExtendConnector(outputDesign, output, leaf));

            transformation.IncomingLinks.Should().SatisfyRespectively(
                link => ShouldRepresentExternalLink(link, leaf, input));
            transformation.OutgoingLinks.Should().BeEmpty();

            transformation.IncomingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [input,], [input,], _valueType));
            transformation.OutgoingNodes.Should().BeEmpty();
        }

        [Fact]
        public void Leaf_node_with_outgoing_link_and_direction_Out()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set");
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.Periodic, 100,
                new AveragePooling(), group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(leaf, _destinationConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*out*");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            ShouldBeEmpty(transformation);
        }

        [Fact]
        public void Leaf_node_with_incoming_and_outgoing_link_and_direction_In()
        {
            var dataPort = CreateDataPort(DataPortDirection.In);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set");
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.OnChange, parent: group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(leaf, _sourceConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*in*");
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _destinationConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            var input = transformation.FunctionBlock.ProcessDataInputs.Should().ContainSingle().Subject;
            ShouldRepresentNode(input, leaf, DataPortDirection.In);
            ShouldHaveNoLinks(input);

            var output = transformation.FunctionBlock.ProcessDataOutputs.Should().ContainSingle().Subject;
            ShouldRepresentNode(output, leaf, DataPortDirection.In);
            ShouldHaveLinkTo(output, _destinationConnector);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    nodeLink => ShouldRepresentNodeLink(nodeLink, input, output)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    outputPooling => ShouldRepresentOutputConfiguration(outputPooling, output)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    transferMode => ShouldRepresentTransferMode<OnChangedTransferMode>(transferMode, output)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                inputDesign => ShouldExtendConnector(inputDesign, input, leaf),
                outputDesign => ShouldExtendConnector(outputDesign, output, leaf));

            transformation.IncomingLinks.Should().SatisfyRespectively(
                link => ShouldRepresentExternalLink(link, leaf, input));
            transformation.OutgoingLinks.Should().BeEmpty();

            transformation.IncomingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [input,], [input,], _valueType));
            transformation.OutgoingNodes.Should().BeEmpty();
        }

        [Fact]
        public void Leaf_node_with_incoming_and_outgoing_link_and_direction_InOut()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set");
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.OnChange, parent: group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _destinationConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(2);
            var inputOut = transformation.FunctionBlock.ProcessDataInputs.ElementAt(0);
            ShouldRepresentNode(inputOut, leaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, inputOut);
            var inputIn = transformation.FunctionBlock.ProcessDataInputs.ElementAt(1);
            ShouldRepresentNode(inputIn, leaf, DataPortDirection.In);
            ShouldHaveNoLinks(inputIn);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(2);
            var outputOut = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(0);
            ShouldRepresentNode(outputOut, leaf, DataPortDirection.Out);
            ShouldHaveNoLinks(outputOut);
            var outputIn = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(1);
            ShouldRepresentNode(outputIn, leaf, DataPortDirection.In);
            ShouldHaveLinkTo(outputIn, _destinationConnector);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    nodeLink1 => ShouldRepresentNodeLink(nodeLink1, inputOut, outputOut),
                    nodeLink2 => ShouldRepresentNodeLink(nodeLink2, inputIn, outputIn)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    outputPooling1 => ShouldRepresentOutputConfiguration(outputPooling1, outputOut),
                    outputPooling2 => ShouldRepresentOutputConfiguration(outputPooling2, outputIn)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    transferMode1 => ShouldRepresentTransferMode<OnChangedTransferMode>(transferMode1, outputOut),
                    transferMode2 => ShouldRepresentTransferMode<OnChangedTransferMode>(transferMode2, outputIn)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                inputDesign1 => ShouldExtendConnector(inputDesign1, inputOut, leaf),
                outputDesign1 => ShouldExtendConnector(outputDesign1, outputOut, leaf),
                inputDesign2 => ShouldExtendConnector(inputDesign2, inputIn, leaf),
                outputDesign2 => ShouldExtendConnector(outputDesign2, outputIn, leaf));

            transformation.IncomingLinks.Should().SatisfyRespectively(
                link => ShouldRepresentExternalLink(link, leaf, inputIn));
            transformation.OutgoingLinks.Should().SatisfyRespectively(
                link => ShouldRepresentExternalLink(link, outputOut));

            transformation.IncomingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [inputIn,], [inputIn,], _valueType));
            transformation.OutgoingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [outputOut,], [outputOut,], _valueType));
        }

        [Fact]
        public void Leaf_node_with_incoming_and_outgoing_link_and_direction_Out()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set");
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.OnChange, parent: group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(leaf, _destinationConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*out*");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            var input = transformation.FunctionBlock.ProcessDataInputs.Should().ContainSingle().Subject;
            ShouldRepresentNode(input, leaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, input);

            var output = transformation.FunctionBlock.ProcessDataOutputs.Should().ContainSingle().Subject;
            ShouldRepresentNode(output, leaf, DataPortDirection.Out);
            ShouldHaveNoLinks(output);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    nodeLink => ShouldRepresentNodeLink(nodeLink, input, output)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    outputPooling => ShouldRepresentOutputConfiguration(outputPooling, output)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    transferMode => ShouldRepresentTransferMode<OnChangedTransferMode>(transferMode, output)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                inputDesign => ShouldExtendConnector(inputDesign, input, leaf),
                outputDesign => ShouldExtendConnector(outputDesign, output, leaf));

            transformation.IncomingLinks.Should().BeEmpty();
            transformation.OutgoingLinks.Should().SatisfyRespectively(
                link => ShouldRepresentExternalLink(link, output));

            transformation.IncomingNodes.Should().BeEmpty();
            transformation.OutgoingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [output,], [output,], _valueType));
        }

        [Fact]
        public void Two_leaf_node_with_different_links_and_direction_InOut()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var leaf1 = AddDataPortTreeNode(dataPort, "Leaf1", "Description1", DataPortTransferMode.OnChange, transferPooling: new SumPooling(), valueType: _valueType);
            var leaf2 = AddDataPortTreeNode(dataPort, "Leaf2", "Description2", DataPortTransferMode.Periodic, 100, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf1, _sourceConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf2, _destinationConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(2);
            var input1 = transformation.FunctionBlock.ProcessDataInputs.ElementAt(0);
            ShouldRepresentNode(input1, leaf1, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, input1);
            var input2 = transformation.FunctionBlock.ProcessDataInputs.ElementAt(1);
            ShouldRepresentNode(input2, leaf2, DataPortDirection.In);
            ShouldHaveNoLinks(input2);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(2);
            var output1 = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(0);
            ShouldRepresentNode(output1, leaf1, DataPortDirection.Out);
            ShouldHaveNoLinks(output1);
            var output2 = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(1);
            ShouldRepresentNode(output2, leaf2, DataPortDirection.In);
            ShouldHaveLinkTo(output2, _destinationConnector);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    nodeLink1 => ShouldRepresentNodeLink(nodeLink1, input1, output1),
                    nodeLink2 => ShouldRepresentNodeLink(nodeLink2, input2, output2)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    outputPooling1 => ShouldRepresentOutputConfiguration<SumPooling>(outputPooling1, output1),
                    outputPooling2 => ShouldRepresentOutputConfiguration(outputPooling2, output2)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    transferMode1 => ShouldRepresentTransferMode<OnChangedTransferMode>(transferMode1, output1),
                    transferMode2 => ShouldRepresentTransferMode(transferMode2, output2, 100.Milliseconds())));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                inputDesign1 => ShouldExtendConnector(inputDesign1, input1, leaf1),
                outputDesign1 => ShouldExtendConnector(outputDesign1, output1, leaf1),
                inputDesign2 => ShouldExtendConnector(inputDesign2, input2, leaf2),
                outputDesign2 => ShouldExtendConnector(outputDesign2, output2, leaf2));

            transformation.IncomingLinks.Should().SatisfyRespectively(
                link => ShouldRepresentExternalLink(link, leaf2, input2));
            transformation.OutgoingLinks.Should().SatisfyRespectively(
                link => ShouldRepresentExternalLink(link, output1));

            transformation.IncomingNodes.Should().SatisfyRespectively(
                leaf1Node => ShouldRepresentExternalCommunicationNode(leaf1Node, leaf1, valueType: _valueType),
                leaf2Node => ShouldRepresentExternalCommunicationNode(leaf2Node, leaf2, [input2,], [input2,], _valueType));
            transformation.OutgoingNodes.Should().SatisfyRespectively(
                leaf1Node => ShouldRepresentExternalCommunicationNode(leaf1Node, leaf1, [output1,], [output1,], _valueType),
                leaf2Node => ShouldRepresentExternalCommunicationNode(leaf2Node, leaf2, valueType: _valueType));
        }

        [Fact]
        public void Creates_complete_node_structure_for_external_communication()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var database = AddDataPortTreeNode(dataPort, "Database");
            var schema = AddDataPortTreeNode(dataPort, "Schema", parent: database);
            var table = AddDataPortTreeNode(dataPort, "Table", "Set", DataPortTransferMode.Periodic, 200,
                new AveragePooling(), schema);
            var primaryKeyColumn = AddDataPortTreeNode(dataPort, "PrimaryKey", parent: table, valueType: _valueType);
            var valueColumn = AddDataPortTreeNode(dataPort, "Value", parent: table, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(valueColumn, _sourceConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            var valueInput = transformation.FunctionBlock.ProcessDataInputs.Should().ContainSingle().Subject;
            ShouldRepresentNode(valueInput, valueColumn, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, valueInput);

            var valueOutput = transformation.FunctionBlock.ProcessDataOutputs.Should().ContainSingle().Subject;
            ShouldRepresentNode(valueOutput, valueColumn, DataPortDirection.Out, $"{table.Name}_1_{valueColumn.Name}");
            ShouldHaveNoLinks(valueOutput);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    valueNodeLink => ShouldRepresentNodeLink(valueNodeLink, valueInput, valueOutput)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    valueNodeLink => ShouldRepresentOutputConfiguration(valueNodeLink, valueOutput)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    valueNodeLink => ShouldRepresentTransferMode<PeriodicTransferMode>(valueNodeLink, valueOutput)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                valueInputDesign => ShouldExtendConnector(valueInputDesign, valueInput, valueColumn),
                valueOutputDesign => ShouldExtendConnector(valueOutputDesign, valueOutput, valueColumn));

            transformation.IncomingLinks.Should().BeEmpty();
            transformation.OutgoingLinks.Should().SatisfyRespectively(
                valueLink => ShouldRepresentExternalLink(valueLink, valueOutput));

            transformation.IncomingNodes.Should().BeEmpty();
            transformation.OutgoingNodes.Should().SatisfyRespectively(
                databaseNode => ShouldRepresentExternalCommunicationNode(databaseNode, database),
                schemaNode => ShouldRepresentExternalCommunicationNode(schemaNode, schema, database),
                tableNode => ShouldRepresentExternalCommunicationNode(tableNode, table, schema, [valueOutput,]),
                primaryKeyNode => ShouldRepresentExternalCommunicationNode(primaryKeyNode, primaryKeyColumn, table, valueType: _valueType),
                valueNode => ShouldRepresentExternalCommunicationNode(valueNode, valueColumn, table, default, [valueOutput,], _valueType));
        }

        [Fact]
        public void Group_node_with_incoming_link_and_direction_In()
        {
            var dataPort = CreateDataPort(DataPortDirection.In);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.OnChange,
                transferPooling: new AveragePooling());
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", transferPooling: new AveragePooling(), parent: group, valueType: _valueType);
            var subGroup = AddDataPortTreeNode(dataPort, "SubGroup", parent: group);
            var subLeaf = AddDataPortTreeNode(dataPort, "SubLeaf", parent: subGroup, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(leaf, _sourceConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*in*");
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(subLeaf, _sourceConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*in*");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            ShouldBeEmpty(transformation);
        }

        [Fact]
        public void Group_node_with_incoming_link_and_direction_InOut()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.OnChange,
                transferPooling: new AveragePooling());
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", transferPooling: new AveragePooling(), parent: group, valueType: _valueType);
            var subGroup = AddDataPortTreeNode(dataPort, "SubGroup", parent: group);
            var subLeaf = AddDataPortTreeNode(dataPort, "SubLeaf", parent: subGroup, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(subLeaf, _sourceConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(2);
            var leafInput = transformation.FunctionBlock.ProcessDataInputs.First();
            ShouldRepresentNode(leafInput, leaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, leafInput);
            var subLeafInput = transformation.FunctionBlock.ProcessDataInputs.Last();
            ShouldRepresentNode(subLeafInput, subLeaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, subLeafInput);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(2);
            var leafOutput = transformation.FunctionBlock.ProcessDataOutputs.First();
            ShouldRepresentNode(leafOutput, leaf, DataPortDirection.Out, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(leafOutput);
            var subLeafOutput = transformation.FunctionBlock.ProcessDataOutputs.Last();
            ShouldRepresentNode(subLeafOutput, subLeaf, DataPortDirection.Out, $"{group.Name}_2_{subLeaf.Name}");
            ShouldHaveNoLinks(subLeafOutput);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentNodeLink(leafNodeLink, leafInput, leafOutput),
                    subLeafNodeLink => ShouldRepresentNodeLink(subLeafNodeLink, subLeafInput, subLeafOutput)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentOutputConfiguration<AveragePooling>(leafNodeLink, leafOutput),
                    subLeafNodeLink => ShouldRepresentOutputConfiguration(subLeafNodeLink, subLeafOutput)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentTransferMode<OnChangedTransferMode>(leafNodeLink, leafOutput),
                    subLeafNodeLink => ShouldRepresentTransferMode<OnChangedTransferMode>(subLeafNodeLink, subLeafOutput)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                leafInputDesign => ShouldExtendConnector(leafInputDesign, leafInput, leaf),
                leafOutputDesign => ShouldExtendConnector(leafOutputDesign, leafOutput, leaf),
                subLeafInputDesign => ShouldExtendConnector(subLeafInputDesign, subLeafInput, subLeaf),
                subLeafOutputDesign => ShouldExtendConnector(subLeafOutputDesign, subLeafOutput, subLeaf));

            transformation.IncomingLinks.Should().BeEmpty();
            transformation.OutgoingLinks.Should().SatisfyRespectively(
                leafLink => ShouldRepresentExternalLink(leafLink, leafOutput),
                subLeafLink => ShouldRepresentExternalLink(subLeafLink, subLeafOutput));

            transformation.IncomingNodes.Should().BeEmpty();
            transformation.OutgoingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [leafOutput, subLeafOutput,]),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, default, [leafOutput,], _valueType),
                subGroupNode => ShouldRepresentExternalCommunicationNode(subGroupNode, subGroup, group),
                subLeafNode => ShouldRepresentExternalCommunicationNode(subLeafNode, subLeaf, subGroup, default, [subLeafOutput,], _valueType));
        }

        [Fact]
        public void Group_node_with_incoming_link_and_direction_Out()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.OnChange,
                transferPooling: new AveragePooling());
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", transferPooling: new AveragePooling(), parent: group, valueType: _valueType);
            var subGroup = AddDataPortTreeNode(dataPort, "SubGroup", parent: group);
            var subLeaf = AddDataPortTreeNode(dataPort, "SubLeaf", parent: subGroup, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(subLeaf, _sourceConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(2);
            var leafInput = transformation.FunctionBlock.ProcessDataInputs.First();
            ShouldRepresentNode(leafInput, leaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, leafInput);
            var subLeafInput = transformation.FunctionBlock.ProcessDataInputs.Last();
            ShouldRepresentNode(subLeafInput, subLeaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, subLeafInput);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(2);
            var leafOutput = transformation.FunctionBlock.ProcessDataOutputs.First();
            ShouldRepresentNode(leafOutput, leaf, DataPortDirection.Out, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(leafOutput);
            var subLeafOutput = transformation.FunctionBlock.ProcessDataOutputs.Last();
            ShouldRepresentNode(subLeafOutput, subLeaf, DataPortDirection.Out, $"{group.Name}_2_{subLeaf.Name}");
            ShouldHaveNoLinks(subLeafOutput);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentNodeLink(leafNodeLink, leafInput, leafOutput),
                    subLeafNodeLink => ShouldRepresentNodeLink(subLeafNodeLink, subLeafInput, subLeafOutput)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentOutputConfiguration<AveragePooling>(leafNodeLink, leafOutput),
                    subLeafNodeLink => ShouldRepresentOutputConfiguration(subLeafNodeLink, subLeafOutput)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentTransferMode<OnChangedTransferMode>(leafNodeLink, leafOutput),
                    subLeafNodeLink => ShouldRepresentTransferMode<OnChangedTransferMode>(subLeafNodeLink, subLeafOutput)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                leafInputDesign => ShouldExtendConnector(leafInputDesign, leafInput, leaf),
                leafOutputDesign => ShouldExtendConnector(leafOutputDesign, leafOutput, leaf),
                subLeafInputDesign => ShouldExtendConnector(subLeafInputDesign, subLeafInput, subLeaf),
                subLeafOutputDesign => ShouldExtendConnector(subLeafOutputDesign, subLeafOutput, subLeaf));

            transformation.IncomingLinks.Should().BeEmpty();
            transformation.OutgoingLinks.Should().SatisfyRespectively(
                leafLink => ShouldRepresentExternalLink(leafLink, leafOutput),
                subLeafLink => ShouldRepresentExternalLink(subLeafLink, subLeafOutput));

            transformation.IncomingNodes.Should().BeEmpty();
            transformation.OutgoingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [leafOutput, subLeafOutput,]),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, default, [leafOutput,], _valueType),
                subGroupNode => ShouldRepresentExternalCommunicationNode(subGroupNode, subGroup, group),
                subLeafNode => ShouldRepresentExternalCommunicationNode(subLeafNode, subLeaf, subGroup, default, [subLeafOutput,], _valueType));
        }

        [Fact]
        public void Group_node_with_outgoing_link_and_direction_In()
        {
            var dataPort = CreateDataPort(DataPortDirection.In);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.Periodic, 100, new AveragePooling());
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", transferPooling: new AveragePooling(), parent: group, valueType: _valueType);
            var subGroup = AddDataPortTreeNode(dataPort, "SubGroup", parent: group);
            var subLeaf = AddDataPortTreeNode(dataPort, "SubLeaf", parent: subGroup, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _destinationConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(subLeaf, _destinationConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(2);
            var leafInput = transformation.FunctionBlock.ProcessDataInputs.First();
            ShouldRepresentNode(leafInput, leaf, DataPortDirection.In, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(leafInput);
            var subLeafInput = transformation.FunctionBlock.ProcessDataInputs.Last();
            ShouldRepresentNode(subLeafInput, subLeaf, DataPortDirection.In, $"{group.Name}_2_{subLeaf.Name}");
            ShouldHaveNoLinks(subLeafInput);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(2);
            var leafOutput = transformation.FunctionBlock.ProcessDataOutputs.First();
            ShouldRepresentNode(leafOutput, leaf, DataPortDirection.In, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveLinkTo(leafOutput, _destinationConnector);
            var subLeafOutput = transformation.FunctionBlock.ProcessDataOutputs.Last();
            ShouldRepresentNode(subLeafOutput, subLeaf, DataPortDirection.In, $"{group.Name}_2_{subLeaf.Name}");
            ShouldHaveLinkTo(subLeafOutput, _destinationConnector);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentNodeLink(leafNodeLink, leafInput, leafOutput),
                    subLeafNodeLink => ShouldRepresentNodeLink(subLeafNodeLink, subLeafInput, subLeafOutput)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentOutputConfiguration<AveragePooling>(leafNodeLink, leafOutput),
                    subLeafNodeLink => ShouldRepresentOutputConfiguration(subLeafNodeLink, subLeafOutput)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentTransferMode(leafNodeLink, leafOutput, 100.Milliseconds()),
                    subLeafNodeLink => ShouldRepresentTransferMode(subLeafNodeLink, subLeafOutput, 100.Milliseconds())));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                leafInputDesign => ShouldExtendConnector(leafInputDesign, leafInput, leaf),
                leafOutputDesign => ShouldExtendConnector(leafOutputDesign, leafOutput, leaf),
                subLeafInputDesign => ShouldExtendConnector(subLeafInputDesign, subLeafInput, subLeaf),
                subLeafOutputDesign => ShouldExtendConnector(subLeafOutputDesign, subLeafOutput, subLeaf));

            transformation.IncomingLinks.Should().SatisfyRespectively(
                leafLink => ShouldRepresentExternalLink(leafLink, leaf, leafInput),
                subLeafLink => ShouldRepresentExternalLink(subLeafLink, subLeaf, subLeafInput));
            transformation.OutgoingLinks.Should().BeEmpty();

            transformation.IncomingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [leafInput, subLeafInput,]),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, default, [leafInput,], _valueType),
                subGroupNode => ShouldRepresentExternalCommunicationNode(subGroupNode, subGroup, group),
                subLeafNode => ShouldRepresentExternalCommunicationNode(subLeafNode, subLeaf, subGroup, default, [subLeafInput,], _valueType));
            transformation.OutgoingNodes.Should().BeEmpty();
        }

        [Fact]
        public void Group_node_with_outgoing_link_and_direction_InOut()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.Periodic, 100, new AveragePooling());
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", transferPooling: new AveragePooling(), parent: group, valueType: _valueType);
            var subGroup = AddDataPortTreeNode(dataPort, "SubGroup", parent: group);
            var subLeaf = AddDataPortTreeNode(dataPort, "SubLeaf", parent: subGroup, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _destinationConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(subLeaf, _destinationConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(2);
            var leafInput = transformation.FunctionBlock.ProcessDataInputs.First();
            ShouldRepresentNode(leafInput, leaf, DataPortDirection.In, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(leafInput);
            var subLeafInput = transformation.FunctionBlock.ProcessDataInputs.Last();
            ShouldRepresentNode(subLeafInput, subLeaf, DataPortDirection.In, $"{group.Name}_2_{subLeaf.Name}");
            ShouldHaveNoLinks(subLeafInput);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(2);
            var leafOutput = transformation.FunctionBlock.ProcessDataOutputs.First();
            ShouldRepresentNode(leafOutput, leaf, DataPortDirection.In, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveLinkTo(leafOutput, _destinationConnector);
            var subLeafOutput = transformation.FunctionBlock.ProcessDataOutputs.Last();
            ShouldRepresentNode(subLeafOutput, subLeaf, DataPortDirection.In, $"{group.Name}_2_{subLeaf.Name}");
            ShouldHaveLinkTo(subLeafOutput, _destinationConnector);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentNodeLink(leafNodeLink, leafInput, leafOutput),
                    subLeafNodeLink => ShouldRepresentNodeLink(subLeafNodeLink, subLeafInput, subLeafOutput)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentOutputConfiguration<AveragePooling>(leafNodeLink, leafOutput),
                    subLeafNodeLink => ShouldRepresentOutputConfiguration(subLeafNodeLink, subLeafOutput)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentTransferMode(leafNodeLink, leafOutput, 100.Milliseconds()),
                    subLeafNodeLink => ShouldRepresentTransferMode(subLeafNodeLink, subLeafOutput, 100.Milliseconds())));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                leafInputDesign => ShouldExtendConnector(leafInputDesign, leafInput, leaf),
                leafOutputDesign => ShouldExtendConnector(leafOutputDesign, leafOutput, leaf),
                subLeafInputDesign => ShouldExtendConnector(subLeafInputDesign, subLeafInput, subLeaf),
                subLeafOutputDesign => ShouldExtendConnector(subLeafOutputDesign, subLeafOutput, subLeaf));

            transformation.IncomingLinks.Should().SatisfyRespectively(
                leafLink => ShouldRepresentExternalLink(leafLink, leaf, leafInput),
                subLeafLink => ShouldRepresentExternalLink(subLeafLink, subLeaf, subLeafInput));
            transformation.OutgoingLinks.Should().BeEmpty();

            transformation.IncomingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [leafInput, subLeafInput,]),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, default, [leafInput,], _valueType),
                subGroupNode => ShouldRepresentExternalCommunicationNode(subGroupNode, subGroup, group),
                subLeafNode => ShouldRepresentExternalCommunicationNode(subLeafNode, subLeaf, subGroup, default, [subLeafInput,], _valueType));
            transformation.OutgoingNodes.Should().BeEmpty();
        }

        [Fact]
        public void Group_node_with_outgoing_link_and_direction_Out()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.Periodic, 100, new AveragePooling());
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", transferPooling: new AveragePooling(), parent: group, valueType: _valueType);
            var subGroup = AddDataPortTreeNode(dataPort, "SubGroup", parent: group);
            var subLeaf = AddDataPortTreeNode(dataPort, "SubLeaf", parent: subGroup, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(leaf, _destinationConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*out*");
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(subLeaf, _destinationConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*out*");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            ShouldBeEmpty(transformation);
        }

        [Fact]
        public void Group_node_with_incoming_and_outgoing_link_and_direction_In()
        {
            var dataPort = CreateDataPort(DataPortDirection.In);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.OnChange);
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", parent: group, valueType: _valueType);
            var subGroup = AddDataPortTreeNode(dataPort, "SubGroup", parent: group);
            var subLeaf = AddDataPortTreeNode(dataPort, "SubLeaf", parent: subGroup, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(leaf, _sourceConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*in*");
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(subLeaf, _sourceConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*in*");
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _destinationConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(subLeaf, _destinationConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(2);
            var leafInput = transformation.FunctionBlock.ProcessDataInputs.First();
            ShouldRepresentNode(leafInput, leaf, DataPortDirection.In, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(leafInput);
            var subLeafInput = transformation.FunctionBlock.ProcessDataInputs.Last();
            ShouldRepresentNode(subLeafInput, subLeaf, DataPortDirection.In, $"{group.Name}_2_{subLeaf.Name}");
            ShouldHaveNoLinks(subLeafInput);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(2);
            var leafOutput = transformation.FunctionBlock.ProcessDataOutputs.First();
            ShouldRepresentNode(leafOutput, leaf, DataPortDirection.In, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveLinkTo(leafOutput, _destinationConnector);
            var subLeafOutput = transformation.FunctionBlock.ProcessDataOutputs.Last();
            ShouldRepresentNode(subLeafOutput, subLeaf, DataPortDirection.In, $"{group.Name}_2_{subLeaf.Name}");
            ShouldHaveLinkTo(subLeafOutput, _destinationConnector);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentNodeLink(leafNodeLink, leafInput, leafOutput),
                    subLeafNodeLink => ShouldRepresentNodeLink(subLeafNodeLink, subLeafInput, subLeafOutput)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentOutputConfiguration(leafNodeLink, leafOutput),
                    subLeafNodeLink => ShouldRepresentOutputConfiguration(subLeafNodeLink, subLeafOutput)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentTransferMode<OnChangedTransferMode>(leafNodeLink, leafOutput),
                    subLeafNodeLink => ShouldRepresentTransferMode<OnChangedTransferMode>(subLeafNodeLink, subLeafOutput)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                leafInputDesign => ShouldExtendConnector(leafInputDesign, leafInput, leaf),
                leafOutputDesign => ShouldExtendConnector(leafOutputDesign, leafOutput, leaf),
                subLeafInputDesign => ShouldExtendConnector(subLeafInputDesign, subLeafInput, subLeaf),
                subLeafOutputDesign => ShouldExtendConnector(subLeafOutputDesign, subLeafOutput, subLeaf));

            transformation.IncomingLinks.Should().SatisfyRespectively(
                leafLink => ShouldRepresentExternalLink(leafLink, leaf, leafInput),
                subLeafLink => ShouldRepresentExternalLink(subLeafLink, subLeaf, subLeafInput));
            transformation.OutgoingLinks.Should().BeEmpty();

            transformation.IncomingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [leafInput, subLeafInput,]),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, default, [leafInput,], _valueType),
                subGroupNode => ShouldRepresentExternalCommunicationNode(subGroupNode, subGroup, group),
                subLeafNode => ShouldRepresentExternalCommunicationNode(subLeafNode, subLeaf, subGroup, default, [subLeafInput,], _valueType));
            transformation.OutgoingNodes.Should().BeEmpty();
        }

        [Fact]
        public void Group_node_with_incoming_and_outgoing_link_and_direction_InOut()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.OnChange);
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", parent: group, valueType: _valueType);
            var subGroup = AddDataPortTreeNode(dataPort, "SubGroup", parent: group);
            var subLeaf = AddDataPortTreeNode(dataPort, "SubLeaf", parent: subGroup, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(subLeaf, _sourceConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _destinationConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(subLeaf, _destinationConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(4);
            var leafInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(0);
            ShouldRepresentNode(leafInput, leaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, leafInput);
            var groupLeafInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(1);
            ShouldRepresentNode(groupLeafInput, leaf, DataPortDirection.In, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(groupLeafInput);
            var subLeafInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(2);
            ShouldRepresentNode(subLeafInput, subLeaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, subLeafInput);
            var groupSubLeafInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(3);
            ShouldRepresentNode(groupSubLeafInput, subLeaf, DataPortDirection.In, $"{group.Name}_2_{subLeaf.Name}");
            ShouldHaveNoLinks(groupSubLeafInput);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(4);
            var groupLeafOutputOut = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(0);
            ShouldRepresentNode(groupLeafOutputOut, leaf, DataPortDirection.Out, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(groupLeafOutputOut);
            var groupLeafOutputIn = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(1);
            ShouldRepresentNode(groupLeafOutputIn, leaf, DataPortDirection.In, $"{group.Name}_2_{leaf.Name}");
            ShouldHaveLinkTo(groupLeafOutputIn, _destinationConnector);
            var groupSubLeafOutputOut = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(2);
            ShouldRepresentNode(groupSubLeafOutputOut, subLeaf, DataPortDirection.Out, $"{group.Name}_3_{subLeaf.Name}");
            ShouldHaveNoLinks(groupSubLeafOutputOut);
            var groupSubLeafOutputIn = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(3);
            ShouldRepresentNode(groupSubLeafOutputIn, subLeaf, DataPortDirection.In, $"{group.Name}_4_{subLeaf.Name}");
            ShouldHaveLinkTo(groupSubLeafOutputIn, _destinationConnector);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    nodeLink => ShouldRepresentNodeLink(nodeLink, leafInput, groupLeafOutputOut),
                    nodeLink => ShouldRepresentNodeLink(nodeLink, groupLeafInput, groupLeafOutputIn),
                    nodeLink => ShouldRepresentNodeLink(nodeLink, subLeafInput, groupSubLeafOutputOut),
                    nodeLink => ShouldRepresentNodeLink(nodeLink, groupSubLeafInput, groupSubLeafOutputIn)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    groupLeafOutputPooling => ShouldRepresentOutputConfiguration(groupLeafOutputPooling, groupLeafOutputOut),
                    groupLeafOutputPooling => ShouldRepresentOutputConfiguration(groupLeafOutputPooling, groupLeafOutputIn),
                    groupSubLeafOutputPooling => ShouldRepresentOutputConfiguration(groupSubLeafOutputPooling, groupSubLeafOutputOut),
                    groupSubLeafOutputPooling => ShouldRepresentOutputConfiguration(groupSubLeafOutputPooling, groupSubLeafOutputIn)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    groupLeafNodeLink => ShouldRepresentTransferMode<OnChangedTransferMode>(groupLeafNodeLink, groupLeafOutputOut),
                    groupLeafNodeLink => ShouldRepresentTransferMode<OnChangedTransferMode>(groupLeafNodeLink, groupLeafOutputIn),
                    groupSubLeafNodeLink => ShouldRepresentTransferMode<OnChangedTransferMode>(groupSubLeafNodeLink, groupSubLeafOutputOut),
                    groupSubLeafNodeLink => ShouldRepresentTransferMode<OnChangedTransferMode>(groupSubLeafNodeLink, groupSubLeafOutputIn)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                leafInputDesign => ShouldExtendConnector(leafInputDesign, leafInput, leaf),
                groupLeafOutputDesignOut => ShouldExtendConnector(groupLeafOutputDesignOut, groupLeafOutputOut, leaf),
                groupLeafInputDesign => ShouldExtendConnector(groupLeafInputDesign, groupLeafInput, leaf),
                groupLeafOutputDesignIn => ShouldExtendConnector(groupLeafOutputDesignIn, groupLeafOutputIn, leaf),
                subLeafInputDesign => ShouldExtendConnector(subLeafInputDesign, subLeafInput, subLeaf),
                groupSubLeafOutputDesignOut => ShouldExtendConnector(groupSubLeafOutputDesignOut, groupSubLeafOutputOut, subLeaf),
                groupSubLeafInputDesign => ShouldExtendConnector(groupSubLeafInputDesign, groupSubLeafInput, subLeaf),
                groupSubLeafOutputDesignIn => ShouldExtendConnector(groupSubLeafOutputDesignIn, groupSubLeafOutputIn, subLeaf));

            transformation.IncomingLinks.Should().SatisfyRespectively(
                groupLeafLink => ShouldRepresentExternalLink(groupLeafLink, leaf, groupLeafInput),
                groupSubLeafLink => ShouldRepresentExternalLink(groupSubLeafLink, subLeaf, groupSubLeafInput));
            transformation.OutgoingLinks.Should().SatisfyRespectively(
                groupLeafLink => ShouldRepresentExternalLink(groupLeafLink, groupLeafOutputOut),
                groupSubLeafLink => ShouldRepresentExternalLink(groupSubLeafLink, groupSubLeafOutputOut));

            transformation.IncomingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [groupLeafInput, groupSubLeafInput,]),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, default, [groupLeafInput,], _valueType),
                subGroupNode => ShouldRepresentExternalCommunicationNode(subGroupNode, subGroup, group),
                subLeafNode => ShouldRepresentExternalCommunicationNode(subLeafNode, subLeaf, subGroup, default, [groupSubLeafInput,], _valueType));
            transformation.OutgoingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [groupLeafOutputOut, groupSubLeafOutputOut,]),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, default, [groupLeafOutputOut,], _valueType),
                subGroupNode => ShouldRepresentExternalCommunicationNode(subGroupNode, subGroup, group),
                subLeafNode => ShouldRepresentExternalCommunicationNode(subLeafNode, subLeaf, subGroup, default, [groupSubLeafOutputOut,], _valueType));
        }

        [Fact]
        public void Group_node_with_incoming_and_outgoing_link_and_direction_Out()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.OnChange);
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", parent: group, valueType: _valueType);
            var subGroup = AddDataPortTreeNode(dataPort, "SubGroup", parent: group);
            var subLeaf = AddDataPortTreeNode(dataPort, "SubLeaf", parent: subGroup, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(subLeaf, _sourceConnector);
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(leaf, _destinationConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*out*");
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(subLeaf, _destinationConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*out*");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(2);
            var leafInput = transformation.FunctionBlock.ProcessDataInputs.First();
            ShouldRepresentNode(leafInput, leaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, leafInput);
            var subLeafInput = transformation.FunctionBlock.ProcessDataInputs.Last();
            ShouldRepresentNode(subLeafInput, subLeaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, subLeafInput);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(2);
            var leafOutput = transformation.FunctionBlock.ProcessDataOutputs.First();
            ShouldRepresentNode(leafOutput, leaf, DataPortDirection.Out, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(leafOutput);
            var subLeafOutput = transformation.FunctionBlock.ProcessDataOutputs.Last();
            ShouldRepresentNode(subLeafOutput, subLeaf, DataPortDirection.Out, $"{group.Name}_2_{subLeaf.Name}");
            ShouldHaveNoLinks(subLeafOutput);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentNodeLink(leafNodeLink, leafInput, leafOutput),
                    subLeafNodeLink => ShouldRepresentNodeLink(subLeafNodeLink, subLeafInput, subLeafOutput)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentOutputConfiguration(leafNodeLink, leafOutput),
                    subLeafNodeLink => ShouldRepresentOutputConfiguration(subLeafNodeLink, subLeafOutput)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    leafNodeLink => ShouldRepresentTransferMode<OnChangedTransferMode>(leafNodeLink, leafOutput),
                    subLeafNodeLink => ShouldRepresentTransferMode<OnChangedTransferMode>(subLeafNodeLink, subLeafOutput)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                leafInputDesign => ShouldExtendConnector(leafInputDesign, leafInput, leaf),
                leafOutputDesign => ShouldExtendConnector(leafOutputDesign, leafOutput, leaf),
                subLeafInputDesign => ShouldExtendConnector(subLeafInputDesign, subLeafInput, subLeaf),
                subLeafOutputDesign => ShouldExtendConnector(subLeafOutputDesign, subLeafOutput, subLeaf));

            transformation.IncomingLinks.Should().BeEmpty();
            transformation.OutgoingLinks.Should().SatisfyRespectively(
                leafLink => ShouldRepresentExternalLink(leafLink, leafOutput),
                subLeafLink => ShouldRepresentExternalLink(subLeafLink, subLeafOutput));

            transformation.IncomingNodes.Should().BeEmpty();
            transformation.OutgoingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [leafOutput, subLeafOutput,]),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, default, [leafOutput,], _valueType),
                subGroupNode => ShouldRepresentExternalCommunicationNode(subGroupNode, subGroup, group),
                subLeafNode => ShouldRepresentExternalCommunicationNode(subLeafNode, subLeaf, subGroup, default, [subLeafOutput,], _valueType));
        }

        [Fact]
        public void Group_and_leaf_node_with_incoming_link_and_direction_In()
        {
            var dataPort = CreateDataPort(DataPortDirection.In);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.Periodic, 50, valueType: _valueType);
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.OnChange,
                transferPooling: new AveragePooling(), parent: group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(group, _sourceConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*in*");
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(leaf, _sourceConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*in*");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            ShouldBeEmpty(transformation);
        }

        [Fact]
        public void Group_and_leaf_node_with_incoming_link_and_direction_InOut()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.Periodic, 50, valueType: _valueType);
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.OnChange,
                transferPooling: new AveragePooling(), parent: group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(group, _sourceConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(2);
            var groupInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(0);
            ShouldRepresentNode(groupInput, group, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, groupInput);
            var leafInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(1);
            ShouldRepresentNode(leafInput, leaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, leafInput);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(3);
            var groupOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(0);
            ShouldRepresentNode(groupOutput, group, DataPortDirection.Out);
            ShouldHaveNoLinks(groupOutput);
            var groupLeafOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(1);
            ShouldRepresentNode(groupLeafOutput, leaf, DataPortDirection.Out, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(groupLeafOutput);
            var leafOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(2);
            ShouldRepresentNode(leafOutput, leaf, DataPortDirection.Out);
            ShouldHaveNoLinks(leafOutput);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    groupNodeLink => ShouldRepresentNodeLink(groupNodeLink, groupInput, groupOutput),
                    groupLeafNodeLink => ShouldRepresentNodeLink(groupLeafNodeLink, leafInput, groupLeafOutput),
                    leafNodeLink => ShouldRepresentNodeLink(leafNodeLink, leafInput, leafOutput)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    groupOutputPooling => ShouldRepresentOutputConfiguration(groupOutputPooling, groupOutput),
                    groupLeafOutputPooling => ShouldRepresentOutputConfiguration<AveragePooling>(groupLeafOutputPooling, groupLeafOutput),
                    leafOutputPooling => ShouldRepresentOutputConfiguration<AveragePooling>(leafOutputPooling, leafOutput)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    groupTransferMode => ShouldRepresentTransferMode(groupTransferMode, groupOutput, 50.Milliseconds()),
                    groupLeafTransferMode => ShouldRepresentTransferMode(groupLeafTransferMode, groupLeafOutput, 50.Milliseconds()),
                    leafTransferMode => ShouldRepresentTransferMode<OnChangedTransferMode>(leafTransferMode, leafOutput)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                groupInputDesign => ShouldExtendConnector(groupInputDesign, groupInput, group),
                groupOutputDesign => ShouldExtendConnector(groupOutputDesign, groupOutput, group),
                leafInputDesign => ShouldExtendConnector(leafInputDesign, leafInput, leaf),
                groupLeafOutputDesign => ShouldExtendConnector(groupLeafOutputDesign, groupLeafOutput, leaf),
                leafOutputDesign => ShouldExtendConnector(leafOutputDesign, leafOutput, leaf));

            transformation.IncomingLinks.Should().BeEmpty();
            transformation.OutgoingLinks.Should().SatisfyRespectively(
                groupLink => ShouldRepresentExternalLink(groupLink, groupOutput),
                groupLeafLink => ShouldRepresentExternalLink(groupLeafLink, groupLeafOutput),
                leafLink => ShouldRepresentExternalLink(leafLink, leafOutput));

            transformation.IncomingNodes.Should().BeEmpty();
            transformation.OutgoingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [groupOutput, groupLeafOutput,], [groupOutput,], _valueType),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [leafOutput,], [groupLeafOutput, leafOutput,], _valueType));
        }

        [Fact]
        public void Group_and_leaf_node_with_incoming_link_and_direction_Out()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.Periodic, 50, valueType: _valueType);
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.OnChange,
                transferPooling: new AveragePooling(), parent: group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(group, _sourceConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(2);
            var groupInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(0);
            ShouldRepresentNode(groupInput, group, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, groupInput);
            var leafInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(1);
            ShouldRepresentNode(leafInput, leaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, leafInput);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(3);
            var groupOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(0);
            ShouldRepresentNode(groupOutput, group, DataPortDirection.Out);
            ShouldHaveNoLinks(groupOutput);
            var groupLeafOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(1);
            ShouldRepresentNode(groupLeafOutput, leaf, DataPortDirection.Out, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(groupLeafOutput);
            var leafOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(2);
            ShouldRepresentNode(leafOutput, leaf, DataPortDirection.Out);
            ShouldHaveNoLinks(leafOutput);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    groupNodeLink => ShouldRepresentNodeLink(groupNodeLink, groupInput, groupOutput),
                    groupLeafNodeLink => ShouldRepresentNodeLink(groupLeafNodeLink, leafInput, groupLeafOutput),
                    leafNodeLink => ShouldRepresentNodeLink(leafNodeLink, leafInput, leafOutput)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    groupOutputPooling => ShouldRepresentOutputConfiguration(groupOutputPooling, groupOutput),
                    groupLeafOutputPooling => ShouldRepresentOutputConfiguration<AveragePooling>(groupLeafOutputPooling, groupLeafOutput),
                    leafOutputPooling => ShouldRepresentOutputConfiguration<AveragePooling>(leafOutputPooling, leafOutput)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    groupTransferMode => ShouldRepresentTransferMode(groupTransferMode, groupOutput, 50.Milliseconds()),
                    groupLeafTransferMode => ShouldRepresentTransferMode(groupLeafTransferMode, groupLeafOutput, 50.Milliseconds()),
                    leafTransferMode => ShouldRepresentTransferMode<OnChangedTransferMode>(leafTransferMode, leafOutput)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                groupInputDesign => ShouldExtendConnector(groupInputDesign, groupInput, group),
                groupOutputDesign => ShouldExtendConnector(groupOutputDesign, groupOutput, group),
                leafInputDesign => ShouldExtendConnector(leafInputDesign, leafInput, leaf),
                groupLeafOutputDesign => ShouldExtendConnector(groupLeafOutputDesign, groupLeafOutput, leaf),
                leafOutputDesign => ShouldExtendConnector(leafOutputDesign, leafOutput, leaf));

            transformation.IncomingLinks.Should().BeEmpty();
            transformation.OutgoingLinks.Should().SatisfyRespectively(
                groupLink => ShouldRepresentExternalLink(groupLink, groupOutput),
                groupLeafLink => ShouldRepresentExternalLink(groupLeafLink, groupLeafOutput),
                leafLink => ShouldRepresentExternalLink(leafLink, leafOutput));

            transformation.IncomingNodes.Should().BeEmpty();
            transformation.OutgoingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [groupOutput, groupLeafOutput,], [groupOutput,], _valueType),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [leafOutput,], [groupLeafOutput, leafOutput,], _valueType));
        }

        [Fact]
        public void Group_and_leaf_node_with_outgoing_link_and_direction_In()
        {
            var dataPort = CreateDataPort(DataPortDirection.In);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.OnChange,
                transferPooling: new SumPooling(), valueType: _valueType);
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.Periodic, 100,
                new AveragePooling(), group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(group, _destinationConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _destinationConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(3);
            var groupInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(0);
            ShouldRepresentNode(groupInput, group, DataPortDirection.In);
            ShouldHaveNoLinks(groupInput);
            var groupLeafInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(1);
            ShouldRepresentNode(groupLeafInput, leaf, DataPortDirection.In, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(groupLeafInput);
            var leafInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(2);
            ShouldRepresentNode(leafInput, leaf, DataPortDirection.In);
            ShouldHaveNoLinks(leafInput);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(3);
            var groupOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(0);
            ShouldRepresentNode(groupOutput, group, DataPortDirection.In);
            ShouldHaveLinkTo(groupOutput, _destinationConnector);
            var groupLeafOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(1);
            ShouldRepresentNode(groupLeafOutput, leaf, DataPortDirection.In, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveLinkTo(groupLeafOutput, _destinationConnector);
            var leafOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(2);
            ShouldRepresentNode(leafOutput, leaf, DataPortDirection.In);
            ShouldHaveLinkTo(leafOutput, _destinationConnector);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    groupNodeLink => ShouldRepresentNodeLink(groupNodeLink, groupInput, groupOutput),
                    groupLeafNodeLink => ShouldRepresentNodeLink(groupLeafNodeLink, groupLeafInput, groupLeafOutput),
                    leafNodeLink => ShouldRepresentNodeLink(leafNodeLink, leafInput, leafOutput)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    groupOutputPooling => ShouldRepresentOutputConfiguration<SumPooling>(groupOutputPooling, groupOutput),
                    groupLeafOutputPooling => ShouldRepresentOutputConfiguration<AveragePooling>(groupLeafOutputPooling, groupLeafOutput),
                    leafOutputPooling => ShouldRepresentOutputConfiguration<AveragePooling>(leafOutputPooling, leafOutput)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    groupTransferMode => ShouldRepresentTransferMode<OnChangedTransferMode>(groupTransferMode, groupOutput),
                    groupLeafTransferMode => ShouldRepresentTransferMode<OnChangedTransferMode>(groupLeafTransferMode, groupLeafOutput),
                    leafTransferMode => ShouldRepresentTransferMode(leafTransferMode, leafOutput, 100.Milliseconds())));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                groupInputDesign => ShouldExtendConnector(groupInputDesign, groupInput, group),
                groupOutputDesign => ShouldExtendConnector(groupOutputDesign, groupOutput, group),
                groupLeafInputDesign => ShouldExtendConnector(groupLeafInputDesign, groupLeafInput, leaf),
                groupLeafOutputDesign => ShouldExtendConnector(groupLeafOutputDesign, groupLeafOutput, leaf),
                leafInputDesign => ShouldExtendConnector(leafInputDesign, leafInput, leaf),
                leafOutputDesign => ShouldExtendConnector(leafOutputDesign, leafOutput, leaf));

            transformation.IncomingLinks.Should().SatisfyRespectively(
                groupLink => ShouldRepresentExternalLink(groupLink, group, groupInput),
                groupLeafLink => ShouldRepresentExternalLink(groupLeafLink, leaf, groupLeafInput),
                leafLink => ShouldRepresentExternalLink(leafLink, leaf, leafInput));
            transformation.OutgoingLinks.Should().BeEmpty();

            transformation.IncomingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [groupInput, groupLeafInput,], [groupInput,], _valueType),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [leafInput,], [groupLeafInput, leafInput,], _valueType));
            transformation.OutgoingNodes.Should().BeEmpty();
        }

        [Fact]
        public void Group_and_leaf_node_with_outgoing_link_and_direction_InOut()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.OnChange,
                transferPooling: new SumPooling(), valueType: _valueType);
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.Periodic, 100,
                new AveragePooling(), group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(group, _destinationConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _destinationConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(3);
            var groupInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(0);
            ShouldRepresentNode(groupInput, group, DataPortDirection.In);
            ShouldHaveNoLinks(groupInput);
            var groupLeafInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(1);
            ShouldRepresentNode(groupLeafInput, leaf, DataPortDirection.In, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(groupLeafInput);
            var leafInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(2);
            ShouldRepresentNode(leafInput, leaf, DataPortDirection.In);
            ShouldHaveNoLinks(leafInput);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(3);
            var groupOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(0);
            ShouldRepresentNode(groupOutput, group, DataPortDirection.In);
            ShouldHaveLinkTo(groupOutput, _destinationConnector);
            var groupLeafOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(1);
            ShouldRepresentNode(groupLeafOutput, leaf, DataPortDirection.In, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveLinkTo(groupLeafOutput, _destinationConnector);
            var leafOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(2);
            ShouldRepresentNode(leafOutput, leaf, DataPortDirection.In);
            ShouldHaveLinkTo(leafOutput, _destinationConnector);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    groupNodeLink => ShouldRepresentNodeLink(groupNodeLink, groupInput, groupOutput),
                    groupLeafNodeLink => ShouldRepresentNodeLink(groupLeafNodeLink, groupLeafInput, groupLeafOutput),
                    leafNodeLink => ShouldRepresentNodeLink(leafNodeLink, leafInput, leafOutput)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    groupOutputPooling => ShouldRepresentOutputConfiguration<SumPooling>(groupOutputPooling, groupOutput),
                    groupLeafOutputPooling => ShouldRepresentOutputConfiguration<AveragePooling>(groupLeafOutputPooling, groupLeafOutput),
                    leafOutputPooling => ShouldRepresentOutputConfiguration<AveragePooling>(leafOutputPooling, leafOutput)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    groupTransferMode => ShouldRepresentTransferMode<OnChangedTransferMode>(groupTransferMode, groupOutput),
                    groupLeafTransferMode => ShouldRepresentTransferMode<OnChangedTransferMode>(groupLeafTransferMode, groupLeafOutput),
                    leafTransferMode => ShouldRepresentTransferMode(leafTransferMode, leafOutput, 100.Milliseconds())));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                groupInputDesign => ShouldExtendConnector(groupInputDesign, groupInput, group),
                groupOutputDesign => ShouldExtendConnector(groupOutputDesign, groupOutput, group),
                groupLeafInputDesign => ShouldExtendConnector(groupLeafInputDesign, groupLeafInput, leaf),
                groupLeafOutputDesign => ShouldExtendConnector(groupLeafOutputDesign, groupLeafOutput, leaf),
                leafInputDesign => ShouldExtendConnector(leafInputDesign, leafInput, leaf),
                leafOutputDesign => ShouldExtendConnector(leafOutputDesign, leafOutput, leaf));

            transformation.IncomingLinks.Should().SatisfyRespectively(
                groupLink => ShouldRepresentExternalLink(groupLink, group, groupInput),
                groupLeafLink => ShouldRepresentExternalLink(groupLeafLink, leaf, groupLeafInput),
                leafLink => ShouldRepresentExternalLink(leafLink, leaf, leafInput));
            transformation.OutgoingLinks.Should().BeEmpty();

            transformation.IncomingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [groupInput, groupLeafInput,], [groupInput,], _valueType),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [leafInput,], [groupLeafInput, leafInput,], _valueType));
            transformation.OutgoingNodes.Should().BeEmpty();
        }

        [Fact]
        public void Group_and_leaf_node_with_outgoing_link_and_direction_Out()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.OnChange,
                transferPooling: new SumPooling(), valueType: _valueType);
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.Periodic, 100,
                new AveragePooling(), group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(group, _destinationConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*out*");
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(leaf, _destinationConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*out*");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            ShouldBeEmpty(transformation);
        }

        [Fact]
        public void Group_and_leaf_node_with_incoming_and_outgoing_link_and_direction_In()
        {
            var dataPort = CreateDataPort(DataPortDirection.In);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.Periodic, 50,
                transferPooling: new SumPooling(), valueType: _valueType);
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.OnChange, parent: group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(group, _sourceConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*in*");
            _builder.Editors.DataPortTreeNode.AssignConnector(group, _destinationConnector);
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(leaf, _sourceConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*in*");
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _destinationConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(3);
            var groupInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(0);
            ShouldRepresentNode(groupInput, group, DataPortDirection.In);
            ShouldHaveNoLinks(groupInput);
            var groupLeafInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(1);
            ShouldRepresentNode(groupLeafInput, leaf, DataPortDirection.In, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(groupLeafInput);
            var leafInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(2);
            ShouldRepresentNode(leafInput, leaf, DataPortDirection.In);
            ShouldHaveNoLinks(leafInput);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(3);
            var groupOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(0);
            ShouldRepresentNode(groupOutput, group, DataPortDirection.In);
            ShouldHaveLinkTo(groupOutput, _destinationConnector);
            var groupLeafOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(1);
            ShouldRepresentNode(groupLeafOutput, leaf, DataPortDirection.In, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveLinkTo(groupLeafOutput, _destinationConnector);
            var leafOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(2);
            ShouldRepresentNode(leafOutput, leaf, DataPortDirection.In);
            ShouldHaveLinkTo(leafOutput, _destinationConnector);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    groupNodeLink => ShouldRepresentNodeLink(groupNodeLink, groupInput, groupOutput),
                    groupLeafNodeLink => ShouldRepresentNodeLink(groupLeafNodeLink, groupLeafInput, groupLeafOutput),
                    leafNodeLink => ShouldRepresentNodeLink(leafNodeLink, leafInput, leafOutput)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    groupOutputPooling => ShouldRepresentOutputConfiguration<SumPooling>(groupOutputPooling, groupOutput),
                    groupLeafOutputPooling => ShouldRepresentOutputConfiguration(groupLeafOutputPooling, groupLeafOutput),
                    leafOutputPooling => ShouldRepresentOutputConfiguration(leafOutputPooling, leafOutput)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    groupTransferMode => ShouldRepresentTransferMode(groupTransferMode, groupOutput, 50.Milliseconds()),
                    groupLeafTransferMode => ShouldRepresentTransferMode(groupLeafTransferMode, groupLeafOutput, 50.Milliseconds()),
                    leafTransferMode => ShouldRepresentTransferMode<OnChangedTransferMode>(leafTransferMode, leafOutput)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                groupInputDesign => ShouldExtendConnector(groupInputDesign, groupInput, group),
                groupOutputDesign => ShouldExtendConnector(groupOutputDesign, groupOutput, group),
                groupLeafInputDesign => ShouldExtendConnector(groupLeafInputDesign, groupLeafInput, leaf),
                groupLeafOutputDesign => ShouldExtendConnector(groupLeafOutputDesign, groupLeafOutput, leaf),
                leafInputDesign => ShouldExtendConnector(leafInputDesign, leafInput, leaf),
                leafOutputDesign => ShouldExtendConnector(leafOutputDesign, leafOutput, leaf));

            transformation.IncomingLinks.Should().SatisfyRespectively(
                groupLink => ShouldRepresentExternalLink(groupLink, group, groupInput),
                groupLeafLink => ShouldRepresentExternalLink(groupLeafLink, leaf, groupLeafInput),
                leafLink => ShouldRepresentExternalLink(leafLink, leaf, leafInput));
            transformation.OutgoingLinks.Should().BeEmpty();

            transformation.IncomingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [groupInput, groupLeafInput,], [groupInput,], _valueType),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [leafInput,], [groupLeafInput, leafInput,], _valueType));
            transformation.OutgoingNodes.Should().BeEmpty();
        }

        [Fact]
        public void Group_and_leaf_node_with_incoming_and_outgoing_link_and_direction_InOut()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.Periodic, 50,
                transferPooling: new SumPooling(), valueType: _valueType);
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.OnChange, parent: group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(group, _sourceConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(group, _destinationConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _destinationConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(5);
            var groupInputOut = transformation.FunctionBlock.ProcessDataInputs.ElementAt(0);
            ShouldRepresentNode(groupInputOut, group, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, groupInputOut);
            var groupInputIn = transformation.FunctionBlock.ProcessDataInputs.ElementAt(1);
            ShouldRepresentNode(groupInputIn, group, DataPortDirection.In);
            ShouldHaveNoLinks(groupInputIn);
            var leafInputOut = transformation.FunctionBlock.ProcessDataInputs.ElementAt(2);
            ShouldRepresentNode(leafInputOut, leaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, leafInputOut);
            var groupLeafInputIn = transformation.FunctionBlock.ProcessDataInputs.ElementAt(3);
            ShouldRepresentNode(groupLeafInputIn, leaf, DataPortDirection.In, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(groupLeafInputIn);
            var leafInputIn = transformation.FunctionBlock.ProcessDataInputs.ElementAt(4);
            ShouldRepresentNode(leafInputIn, leaf, DataPortDirection.In);
            ShouldHaveNoLinks(leafInputIn);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(6);
            var groupOutputOut = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(0);
            ShouldRepresentNode(groupOutputOut, group, DataPortDirection.Out);
            ShouldHaveNoLinks(groupOutputOut);
            var groupOutputIn = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(1);
            ShouldRepresentNode(groupOutputIn, group, DataPortDirection.In);
            ShouldHaveLinkTo(groupOutputIn, _destinationConnector);
            var groupLeafOutputOut = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(2);
            ShouldRepresentNode(groupLeafOutputOut, leaf, DataPortDirection.Out, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(groupLeafOutputOut);
            var groupLeafOutputIn = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(3);
            ShouldRepresentNode(groupLeafOutputIn, leaf, DataPortDirection.In, $"{group.Name}_2_{leaf.Name}");
            ShouldHaveLinkTo(groupLeafOutputIn, _destinationConnector);
            var leafOutputOut = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(4);
            ShouldRepresentNode(leafOutputOut, leaf, DataPortDirection.Out);
            ShouldHaveNoLinks(leafOutputOut);
            var leafOutputIn = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(5);
            ShouldRepresentNode(leafOutputIn, leaf, DataPortDirection.In);
            ShouldHaveLinkTo(leafOutputIn, _destinationConnector);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    groupNodeLinkOut => ShouldRepresentNodeLink(groupNodeLinkOut, groupInputOut, groupOutputOut),
                    groupNodeLinkIn => ShouldRepresentNodeLink(groupNodeLinkIn, groupInputIn, groupOutputIn),
                    groupLeafNodeLinkOut => ShouldRepresentNodeLink(groupLeafNodeLinkOut, leafInputOut, groupLeafOutputOut),
                    groupLeafNodeLinkIn => ShouldRepresentNodeLink(groupLeafNodeLinkIn, groupLeafInputIn, groupLeafOutputIn),
                    leafNodeLinkOut => ShouldRepresentNodeLink(leafNodeLinkOut, leafInputOut, leafOutputOut),
                    leafNodeLinkIn => ShouldRepresentNodeLink(leafNodeLinkIn, leafInputIn, leafOutputIn)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    groupOutputPoolingOut => ShouldRepresentOutputConfiguration<SumPooling>(groupOutputPoolingOut, groupOutputOut),
                    groupOutputPoolingIn => ShouldRepresentOutputConfiguration<SumPooling>(groupOutputPoolingIn, groupOutputIn),
                    groupLeafOutputPoolingOut => ShouldRepresentOutputConfiguration(groupLeafOutputPoolingOut, groupLeafOutputOut),
                    groupLeafOutputPoolingIn => ShouldRepresentOutputConfiguration(groupLeafOutputPoolingIn, groupLeafOutputIn),
                    leafOutputPoolingOut => ShouldRepresentOutputConfiguration(leafOutputPoolingOut, leafOutputOut),
                    leafOutputPoolingIn => ShouldRepresentOutputConfiguration(leafOutputPoolingIn, leafOutputIn)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    groupTransferModeOut => ShouldRepresentTransferMode(groupTransferModeOut, groupOutputOut, 50.Milliseconds()),
                    groupTransferModeIn => ShouldRepresentTransferMode(groupTransferModeIn, groupOutputIn, 50.Milliseconds()),
                    groupLeafTransferModeOut => ShouldRepresentTransferMode(groupLeafTransferModeOut, groupLeafOutputOut, 50.Milliseconds()),
                    groupLeafTransferModeIn => ShouldRepresentTransferMode(groupLeafTransferModeIn, groupLeafOutputIn, 50.Milliseconds()),
                    leafTransferModeOut => ShouldRepresentTransferMode<OnChangedTransferMode>(leafTransferModeOut, leafOutputOut),
                    leafTransferModeIn => ShouldRepresentTransferMode<OnChangedTransferMode>(leafTransferModeIn, leafOutputIn)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                groupInputDesignOut => ShouldExtendConnector(groupInputDesignOut, groupInputOut, group),
                groupOutputDesignOut => ShouldExtendConnector(groupOutputDesignOut, groupOutputOut, group),
                groupInputDesignIn => ShouldExtendConnector(groupInputDesignIn, groupInputIn, group),
                groupOutputDesignIn => ShouldExtendConnector(groupOutputDesignIn, groupOutputIn, group),
                leafInputDesignOut => ShouldExtendConnector(leafInputDesignOut, leafInputOut, leaf),
                groupLeafOutputDesignOut => ShouldExtendConnector(groupLeafOutputDesignOut, groupLeafOutputOut, leaf),
                groupLeafInputDesignIn => ShouldExtendConnector(groupLeafInputDesignIn, groupLeafInputIn, leaf),
                groupLeafOutputDesignIn => ShouldExtendConnector(groupLeafOutputDesignIn, groupLeafOutputIn, leaf),
                leafOutputDesignOut => ShouldExtendConnector(leafOutputDesignOut, leafOutputOut, leaf),
                leafInputDesignIn => ShouldExtendConnector(leafInputDesignIn, leafInputIn, leaf),
                leafOutputDesignIn => ShouldExtendConnector(leafOutputDesignIn, leafOutputIn, leaf));

            transformation.IncomingLinks.Should().SatisfyRespectively(
                groupLink => ShouldRepresentExternalLink(groupLink, group, groupInputIn),
                groupLeafLink => ShouldRepresentExternalLink(groupLeafLink, leaf, groupLeafInputIn),
                leafLink => ShouldRepresentExternalLink(leafLink, leaf, leafInputIn));
            transformation.OutgoingLinks.Should().SatisfyRespectively(
                groupLink => ShouldRepresentExternalLink(groupLink, groupOutputOut),
                groupLeafLink => ShouldRepresentExternalLink(groupLeafLink, groupLeafOutputOut),
                leafLink => ShouldRepresentExternalLink(leafLink, leafOutputOut));

            transformation.IncomingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [groupInputIn, groupLeafInputIn,], [groupInputIn,], _valueType),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [leafInputIn,], [groupLeafInputIn, leafInputIn,], _valueType));
            transformation.OutgoingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [groupOutputOut, groupLeafOutputOut,], [groupOutputOut,], _valueType),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [leafOutputOut,], [groupLeafOutputOut, leafOutputOut,], _valueType));
        }

        [Fact]
        public void Group_and_leaf_node_with_incoming_and_outgoing_link_and_direction_Out()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var group = AddDataPortTreeNode(dataPort, "Group", "Set", DataPortTransferMode.Periodic, 50,
                transferPooling: new SumPooling(), valueType: _valueType);
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.OnChange, parent: group, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AssignConnector(group, _sourceConnector);
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(group, _destinationConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*out*");
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);
            _builder.Editors.DataPortTreeNode.Invoking(x => x.AssignConnector(leaf, _destinationConnector))
                .Should().Throw<ArgumentException>().WithParameterName("connector").WithMessage("*can't*assign*connector*DataPort*direction*out*");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(2);
            var groupInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(0);
            ShouldRepresentNode(groupInput, group, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, groupInput);
            var leafInput = transformation.FunctionBlock.ProcessDataInputs.ElementAt(1);
            ShouldRepresentNode(leafInput, leaf, DataPortDirection.Out);
            ShouldHaveLinkFrom(_sourceConnector, leafInput);

            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(3);
            var groupOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(0);
            ShouldRepresentNode(groupOutput, group, DataPortDirection.Out);
            ShouldHaveNoLinks(groupOutput);
            var groupLeafOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(1);
            ShouldRepresentNode(groupLeafOutput, leaf, DataPortDirection.Out, $"{group.Name}_1_{leaf.Name}");
            ShouldHaveNoLinks(groupLeafOutput);
            var leafOutput = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(2);
            ShouldRepresentNode(leafOutput, leaf, DataPortDirection.Out);
            ShouldHaveNoLinks(leafOutput);

            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().SatisfyRespectively(
                    groupNodeLink => ShouldRepresentNodeLink(groupNodeLink, groupInput, groupOutput),
                    groupLeafNodeLink => ShouldRepresentNodeLink(groupLeafNodeLink, leafInput, groupLeafOutput),
                    leafNodeLink => ShouldRepresentNodeLink(leafNodeLink, leafInput, leafOutput)),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().SatisfyRespectively(
                    groupOutputPooling => ShouldRepresentOutputConfiguration<SumPooling>(groupOutputPooling, groupOutput),
                    groupLeafOutputPooling => ShouldRepresentOutputConfiguration(groupLeafOutputPooling, groupLeafOutput),
                    leafOutputPooling => ShouldRepresentOutputConfiguration(leafOutputPooling, leafOutput)),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().SatisfyRespectively(
                    groupTransferMode => ShouldRepresentTransferMode(groupTransferMode, groupOutput, 50.Milliseconds()),
                    groupLeafTransferMode => ShouldRepresentTransferMode(groupLeafTransferMode, groupLeafOutput, 50.Milliseconds()),
                    leafTransferMode => ShouldRepresentTransferMode<OnChangedTransferMode>(leafTransferMode, leafOutput)));

            transformation.AdditionalConnectorDesigns.Should().SatisfyRespectively(
                groupInputDesign => ShouldExtendConnector(groupInputDesign, groupInput, group),
                groupOutputDesign => ShouldExtendConnector(groupOutputDesign, groupOutput, group),
                leafInputDesign => ShouldExtendConnector(leafInputDesign, leafInput, leaf),
                groupLeafOutputDesign => ShouldExtendConnector(groupLeafOutputDesign, groupLeafOutput, leaf),
                leafOutputDesign => ShouldExtendConnector(leafOutputDesign, leafOutput, leaf));

            transformation.IncomingLinks.Should().BeEmpty();
            transformation.OutgoingLinks.Should().SatisfyRespectively(
                groupLink => ShouldRepresentExternalLink(groupLink, groupOutput),
                groupLeafLink => ShouldRepresentExternalLink(groupLeafLink, groupLeafOutput),
                leafLink => ShouldRepresentExternalLink(leafLink, leafOutput));

            transformation.IncomingNodes.Should().BeEmpty();
            transformation.OutgoingNodes.Should().SatisfyRespectively(
                groupNode => ShouldRepresentExternalCommunicationNode(groupNode, group, [groupOutput, groupLeafOutput,], [groupOutput,], _valueType),
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, group, [leafOutput,], [groupLeafOutput, leafOutput,], _valueType));
        }

        [Fact]
        public void Uses_most_used_data_type_for_node_with_incoming_and_outgoing_links()
        {
            var functionBlockDesign = FunctionBlockDesignBuilder.Create(name: "OtherFB");
            functionBlockDesign.AddInput<float>();
            functionBlockDesign.AddInput<double>();
            functionBlockDesign.AddOutput<double>();

            var dataPortFunctionBlockDesign = FunctionBlockDesignBuilder.Create(DataPortTransformer.DataPortFbDesignId, "DataPort");
            dataPortFunctionBlockDesign.AddSetting<List<NodeLink>>(DataPortTransformer.NodeLinksSettingId);
            dataPortFunctionBlockDesign.AddSetting<Dictionary<Guid, OutputConfiguration>>(DataPortTransformer.OutputConfigurationsSettingId);
            dataPortFunctionBlockDesign.AddSetting<Dictionary<Guid, ITransferMode>>(DataPortTransformer.TransferModesSettingId);

            using ClusterBuilder builder = new(new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([dataPortFunctionBlockDesign, functionBlockDesign,])));
            builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(dataPortFunctionBlockDesign.Id);
            builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(functionBlockDesign.Id);

            _ = builder.CreateTopologyToEngine("engine");
            var dataflow = builder.Cluster.Dataflows.First();

            var dataPort = builder.Editors.Dataflow.AddDataPort(dataflow, Constants.MqttDataPortDesignId, "dataPort", DataPortDirection.InOut, Constants.MqttRuleSetId);

            var functionBlock = builder.Editors.Container.AddFunctionBlock(dataflow.Root, functionBlockDesign.Id);
            var sourceConnector = functionBlock.ProcessDataOutputs.First();
            var sourceConnectorType = builder.ResolveConnectorDesignSourceType(sourceConnector);
            var destinationConnector1 = functionBlock.ProcessDataInputs.First();
            var destinationConnector2 = functionBlock.ProcessDataInputs.Last();

            var leaf = builder.Editors.DataPort.AddTreeNode(Constants.MqttDataPortTopicNodeId, dataPort, "Node", sourceConnectorType, DataPortTransferMode.OnChange);
            builder.Editors.DataPortTreeNode.AssignConnector(leaf, sourceConnector);
            builder.Editors.DataPortTreeNode.AssignConnector(leaf, destinationConnector1);
            builder.Editors.DataPortTreeNode.AssignConnector(leaf, destinationConnector2);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, builder, _rulesetProvider);

            transformation.AdditionalConnectorDesigns.Should().AllSatisfy(x => x.ValueType.Should().Be<double>());
        }

        [Fact]
        public void Transfers_node_properties_to_external_communication_node()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var leaf = AddDataPortTreeNode(dataPort, "Leaf", "Description", DataPortTransferMode.OnChange, valueType: _valueType);
            _builder.Editors.DataPortTreeNode.AddProperty("Retain", leaf, value: true);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _sourceConnector);
            _builder.Editors.DataPortTreeNode.AssignConnector(leaf, _destinationConnector);

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(2);
            var input1 = transformation.FunctionBlock.ProcessDataInputs.ElementAt(0);
            var input2 = transformation.FunctionBlock.ProcessDataInputs.ElementAt(1);
            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(2);
            var output1 = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(0);
            var output2 = transformation.FunctionBlock.ProcessDataOutputs.ElementAt(1);
            transformation.IncomingNodes.Should().SatisfyRespectively(
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, [input2,], [input2,], _valueType,
                new()
                {
                    ["Retain"] = new() { Value = true, },
                }));
            transformation.OutgoingNodes.Should().SatisfyRespectively(
                leafNode => ShouldRepresentExternalCommunicationNode(leafNode, leaf, [output1,], [output1,], _valueType,
                new()
                {
                    ["Retain"] = new() { Value = true, },
                }));
        }

        private DataPortTreeNode AddDataPortTreeNode(DataPort dataPort, string name,
            string? description = default, DataPortTransferMode transferMode = DataPortTransferMode.None, uint? transferInterval = default,
            IAggregatingPooling? transferPooling = default, DataPortTreeNode? parent = default, Type? valueType = null)
        {
            var node = parent is not null
                ? _builder.Editors.DataPortTreeNode.AddTreeNode(Constants.MqttDataPortTopicNodeId, parent, name, valueType, transferMode)
                : _builder.Editors.DataPort.AddTreeNode(Constants.MqttDataPortTopicNodeId, dataPort, name, valueType, transferMode);
            if (description is not null)
                _builder.Editors.DataPortTreeNode.SetDescription(node, description);
            if (transferInterval.HasValue)
                _builder.Editors.DataPortTreeNode.SetTransferIntervalInMs(node, transferInterval.Value);
            if (transferPooling is not null)
                _builder.Editors.DataPortTreeNode.SetTransferPooling(node, transferPooling);
            return node;
        }

        private static void ShouldRepresentNode(ConnectorInput input, DataPortTreeNode leaf, DataPortDirection direction, string? name = default)
        {
            input.Id.Should().NotBe(leaf.Id);
            input.Name.Should().Be($"{name ?? leaf.Name}_{direction}");
            input.ShortName.Should().BeEmpty();
            input.Description.Should().Be(leaf.Description);
            if (leaf.TransferPooling is not null)
            {
                input.CrossSourcePoolingStrategy.Should().Be(PoolingStrategy.Aggregate);
                input.PerSourcePoolingStrategy.Should().Be(PoolingStrategy.Aggregate);
            }
            else
            {
                input.CrossSourcePoolingStrategy.Should().Be(PoolingStrategy.Queue);
                input.PerSourcePoolingStrategy.Should().Be(PoolingStrategy.Queue);
            }

            input.CrossSourceAggregatingPooling.Should().BeSameAs(leaf.TransferPooling);
            input.PerSourceAggregatingPooling.Should().BeSameAs(leaf.TransferPooling);
            input.Type.Should().Be(ConnectorType.ProcessData);
            input.Validators.Should().BeEmpty();
            input.Published.Should().BeFalse();
            input.EventEnabled.Should().BeFalse();
        }

        private static void ShouldRepresentNode(ConnectorOutput output, DataPortTreeNode leaf, DataPortDirection direction, string? name = default)
        {
            output.Id.Should().NotBe(leaf.Id);
            output.Name.Should().Be($"{name ?? leaf.Name}_{direction}");
            output.ShortName.Should().BeEmpty();
            output.Description.Should().Be(leaf.Description);
            output.Type.Should().Be(ConnectorType.ProcessData);
            output.Validators.Should().BeEmpty();
            output.Published.Should().BeFalse();
            output.EventEnabled.Should().BeFalse();
        }

        private static void ShouldHaveNoLinks(Connector connector)
        {
            connector.Links.Should().BeEmpty();
            connector.Value.Should().BeNull();
        }

        private static void ShouldHaveLinkFrom(ConnectorOutput output, ConnectorInput input)
        {
            var link = input.Links.Should().ContainSingle().Subject;
            ShouldConnectConnectors(link, output, input);
        }

        private static void ShouldHaveLinkTo(ConnectorOutput output, ConnectorInput input)
        {
            var link = output.Links.Should().ContainSingle().Subject;
            ShouldConnectConnectors(link, output, input);
        }

        private static void ShouldConnectConnectors(Link link, ConnectorOutput output, ConnectorInput input)
        {
            link.SourceConnectorId.Should().Be(output.Id);
            link.SourceConnector.Should().BeSameAs(output);
            link.DestinationConnector.Should().BeSameAs(input);
            input.Value.Should().BeSameAs(output.Value);
        }

        private static void ShouldRepresentNodeLink(NodeLink nodeLink, ConnectorInput input, ConnectorOutput output)
        {
            nodeLink.InputConnectorId.Should().Be(input.Id);
            nodeLink.OutputConnectorId.Should().Be(output.Id);
        }

        private static void ShouldRepresentOutputConfiguration(KeyValuePair<Guid, OutputConfiguration> outputConfiguration, ConnectorOutput output)
        {
            outputConfiguration.Key.Should().Be(output.Id);
            outputConfiguration.Value.Pooling.CrossSourcePoolingStrategy.Should().Be(PoolingStrategy.Queue);
            outputConfiguration.Value.Pooling.CrossSourceAggregatingPooling.Should().BeNull();
            outputConfiguration.Value.Pooling.PerSourcePoolingStrategy.Should().Be(PoolingStrategy.Queue);
            outputConfiguration.Value.Pooling.PerSourceAggregatingPooling.Should().BeNull();
            outputConfiguration.Value.MarkAsChangedIfNotEqual.Should().BeFalse();
        }

        private static void ShouldRepresentOutputConfiguration<T>(KeyValuePair<Guid, OutputConfiguration> outputConfiguration, ConnectorOutput output)
        {
            outputConfiguration.Key.Should().Be(output.Id);
            outputConfiguration.Value.Pooling.CrossSourcePoolingStrategy.Should().Be(PoolingStrategy.Aggregate);
            outputConfiguration.Value.Pooling.CrossSourceAggregatingPooling.Should().BeOfType<T>();
            outputConfiguration.Value.Pooling.PerSourcePoolingStrategy.Should().Be(PoolingStrategy.Aggregate);
            outputConfiguration.Value.Pooling.PerSourceAggregatingPooling.Should().BeOfType<T>();
            outputConfiguration.Value.MarkAsChangedIfNotEqual.Should().BeFalse();
        }

        private static void ShouldRepresentTransferMode<T>(KeyValuePair<Guid, ITransferMode> transferMode, ConnectorOutput output)
        {
            transferMode.Key.Should().Be(output.Id);
            transferMode.Value.Should().BeOfType<T>();
        }

        private static void ShouldRepresentTransferMode(KeyValuePair<Guid, ITransferMode> transferMode, ConnectorOutput output, TimeSpan interval)
        {
            transferMode.Key.Should().Be(output.Id);
            transferMode.Value.Should().BeOfType<PeriodicTransferMode>().Which.Interval.Should().Be(interval);
        }

        private void ShouldExtendConnector(FunctionBlockConnectorDesign design, Connector connector, DataPortTreeNode node)
        {
            design.ConnectorId.Should().Be(connector.Id);
            design.IsInput.Should().Be(connector is ConnectorInput);
            design.Name.Should().Be($"_{node.Id:n}");
            design.ValueType.Should().Be(_valueType);
        }

        private static void ShouldRepresentExternalLink(IncomingLink link, DataPortTreeNode node, ConnectorInput connector)
        {
            link.ConnectorId.Should().Be(connector.Id);
            link.RemoteId.Should().Be(connector.Id);
            link.SourceType.Should().Be(node.ValueType!.AssemblyQualifiedName);
            link.TypeConverter.Should().BeNull();
        }

        private static void ShouldRepresentExternalLink(OutgoingLink link, ConnectorOutput connector)
        {
            link.ConnectorId.Should().Be(connector.Id);
            link.RemoteId.Should().Be(connector.Id);
        }

        private static void ShouldRepresentExternalCommunicationNode(Node node, DataPortTreeNode treeNode,
            IEnumerable<Connector> transferredChannels, IEnumerable<Connector>? affectedChannels = default, Type? valueType = default,
            Dictionary<string, Property>? properties = default)
            => ShouldRepresentExternalCommunicationNode(node, treeNode, default, transferredChannels, affectedChannels, valueType, properties);
        private static void ShouldRepresentExternalCommunicationNode(Node node, DataPortTreeNode treeNode, DataPortTreeNode? parent = default,
            IEnumerable<Connector>? transferredChannels = default, IEnumerable<Connector>? affectedChannels = default, Type? valueType = default,
            Dictionary<string, Property>? properties = default)
        {
            node.Id.Should().Be(treeNode.Id);

            // TODO actually this ID is fixed - no mapping available actually
            //node.DesignId.Should().BeEquivalentTo(treeNode.DesignId);
            node.Name.Should().Be(treeNode.Name);
            if (parent is null)
                node.ParentId.Should().BeNull();
            else
                node.ParentId.Should().Be(parent.Id);
            if (affectedChannels is null)
                node.AffectedChannels.Should().BeEmpty();
            else
                node.AffectedChannels.Should().BeEquivalentTo(affectedChannels.Select(c => c.Id.ToString()));
            if (transferredChannels is null)
                node.TransferredChannels.Should().BeEmpty();
            else
                node.TransferredChannels.Should().BeEquivalentTo(transferredChannels.Select(c => c.Id.ToString()));
            if (valueType is null)
                node.ValueType.Should().BeNull();
            else
                node.ValueType.Should().Be(valueType);
            if (properties is null)
                node.Properties.Should().BeEmpty();
            else
                node.Properties.Should().BeEquivalentTo(properties);
        }

        private static void ShouldBeEmpty(DataPortTransformation transformation)
        {
            transformation.FunctionBlock.ProcessDataInputs.Should().BeEmpty();
            transformation.FunctionBlock.ProcessDataOutputs.Should().BeEmpty();
            transformation.FunctionBlock.Settings.Should().SatisfyRespectively(
                nodeLinks => nodeLinks.Value.Should().BeOfType<List<NodeLink>>().Which.Should().BeEmpty(),
                outputPoolings => outputPoolings.Value.Should().BeOfType<Dictionary<Guid, OutputConfiguration>>().Which.Should().BeEmpty(),
                transferModes => transferModes.Value.Should().BeOfType<Dictionary<Guid, ITransferMode>>().Which.Should().BeEmpty());
            transformation.AdditionalConnectorDesigns.Should().BeEmpty();
            transformation.IncomingLinks.Should().BeEmpty();
            transformation.OutgoingLinks.Should().BeEmpty();
            transformation.IncomingNodes.Should().BeEmpty();
            transformation.OutgoingNodes.Should().BeEmpty();
        }
    }

    public sealed class TransformToFunctionBlock_EnvelopeChildren : IDisposable
    {
        private const string FolderDesignId = "Folder";
        private const string DataPointFloatDesignId = "DataPointFloat";
        private const string DataPointBinaryDesignId = "DataPointBinary";
        private const string UserPropertyDesignId = "UserProperty";
        private const string TimestampDesignId = "Timestamp";
        private const string ValidityDesignId = "Validity";
        private const string EngineCycleDesignId = "EngineCycle";
        private const string OutboundFolderDesignId = "OutboundFolder";
        private const string DataPointStringDesignId = "DataPointString";

        private readonly ClusterBuilder _builder;
        private readonly ISuiteRulesetProvider _rulesetProvider;
        private readonly FunctionBlock _dataflowFunctionBlock;

        public TransformToFunctionBlock_EnvelopeChildren()
        {
            var dataflowFunctionBlockDesign = FunctionBlockDesignBuilder.Create(name: "Dataflow");
            dataflowFunctionBlockDesign.AddOutput<double>(name: "pressure");
            dataflowFunctionBlockDesign.AddOutput<DateTime>(name: "timestamp");
            dataflowFunctionBlockDesign.AddOutput<string>(name: "batchId");
            dataflowFunctionBlockDesign.AddOutput<byte[]>(name: "image");
            dataflowFunctionBlockDesign.AddOutput<bool>(name: "valid");
            dataflowFunctionBlockDesign.AddOutput<long>(name: "cycle");
            dataflowFunctionBlockDesign.AddInput<double>(name: "pressure");
            dataflowFunctionBlockDesign.AddInput<DateTime>(name: "timestamp");
            dataflowFunctionBlockDesign.AddInput<string>(name: "batchId");
            dataflowFunctionBlockDesign.AddInput<byte[]>(name: "image");
            dataflowFunctionBlockDesign.AddInput<long>(name: "cycle");

            var dataPortFunctionBlockDesign = FunctionBlockDesignBuilder.CreateDataPortFunctionBlockDesign();

            _builder = new(new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([dataPortFunctionBlockDesign, dataflowFunctionBlockDesign,])));
            _builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(dataPortFunctionBlockDesign.Id);
            _builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(dataflowFunctionBlockDesign.Id);

            _rulesetProvider = Substitute.For<ISuiteRulesetProvider>();
            _rulesetProvider.GetRuleset(Arg.Any<string>()).Returns(RulesetFactory.CreateTestRulesetMqttWithEnvelopeChildren());

            _dataflowFunctionBlock = _builder.Editors.Container.AddFunctionBlock(_builder.Cluster.Dataflows.First().Root, dataflowFunctionBlockDesign.Id);
        }

        public void Dispose() => _builder.Dispose();

        [Fact]
        public void Parent_keeps_its_own_channel_and_transfers_the_channels_of_its_children()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var (pressure, timestamp, batchId) = AddPressureWithChildren(dataPort, DataPortTransferMode.OnChange);
            WriteFromDataflow(pressure, "pressure");
            WriteFromDataflow(timestamp, "timestamp");
            WriteFromDataflow(batchId, "batchId");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.FunctionBlock.ProcessDataInputs.Should().HaveCount(3);
            transformation.FunctionBlock.ProcessDataOutputs.Should().HaveCount(3);
            var pressureNode = transformation.OutgoingNodes.Single(n => n.Id == pressure.Id);
            var timestampNode = transformation.OutgoingNodes.Single(n => n.Id == timestamp.Id);
            var batchIdNode = transformation.OutgoingNodes.Single(n => n.Id == batchId.Id);
            pressureNode.AffectedChannels.Should().ContainSingle();
            pressureNode.TransferredChannels.Should().BeEquivalentTo([.. pressureNode.AffectedChannels, .. timestampNode.AffectedChannels, .. batchIdNode.AffectedChannels,]);
            timestampNode.AffectedChannels.Should().ContainSingle();
            timestampNode.TransferredChannels.Should().BeEmpty();
            batchIdNode.AffectedChannels.Should().ContainSingle();
            batchIdNode.TransferredChannels.Should().BeEmpty();
            transformation.OutgoingLinks.Select(l => l.ConnectorId.ToString()).Should().BeEquivalentTo(pressureNode.TransferredChannels);
        }

        [Fact]
        public void Children_transfer_with_the_mode_of_the_parent()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var (pressure, timestamp, batchId) = AddPressureWithChildren(dataPort, DataPortTransferMode.Periodic, transferIntervalInMs: 500);
            WriteFromDataflow(pressure, "pressure");
            WriteFromDataflow(timestamp, "timestamp");
            WriteFromDataflow(batchId, "batchId");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            var pressureNode = transformation.OutgoingNodes.Single(n => n.Id == pressure.Id);
            var transferModes = GetTransferModes(transformation);
            transferModes.Keys.Select(k => k.ToString()).Should().BeEquivalentTo(pressureNode.TransferredChannels);
            transferModes.Values.Should().AllSatisfy(mode => mode.Should().BeOfType<PeriodicTransferMode>().Which.Interval.Should().Be(500.Milliseconds()));
        }

        [Fact]
        public void Children_of_an_incoming_parent_get_their_own_channel_the_same_way()
        {
            var dataPort = CreateDataPort(DataPortDirection.In);
            var (pressure, _, batchId) = AddPressureWithChildren(dataPort, DataPortTransferMode.OnChange);
            ReadIntoDataflow(pressure, "pressure");
            ReadIntoDataflow(batchId, "batchId");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            var pressureNode = transformation.IncomingNodes.Single(n => n.Id == pressure.Id);
            var batchIdNode = transformation.IncomingNodes.Single(n => n.Id == batchId.Id);
            pressureNode.AffectedChannels.Should().ContainSingle();
            pressureNode.TransferredChannels.Should().BeEquivalentTo([.. pressureNode.AffectedChannels, .. batchIdNode.AffectedChannels,]);
            batchIdNode.TransferredChannels.Should().BeEmpty();
            transformation.IncomingLinks.Select(l => l.ConnectorId.ToString()).Should().BeEquivalentTo(pressureNode.TransferredChannels);
        }

        [Fact]
        public void Fails_if_a_child_is_read_by_the_dataflow_but_the_parent_type_only_transfers_outbound()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var image = AddChild(AddRootFolder(dataPort), DataPointBinaryDesignId, "image", typeof(byte[]), DataPortTransferMode.OnChange);
            var batchId = AddChild(image, UserPropertyDesignId, "batchId", typeof(string));
            WriteFromDataflow(image, "image");
            ReadIntoDataflow(batchId, "batchId");

            var act = FluentActions.Invoking(() => DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider));

            act.Should().Throw<InvalidOperationException>().WithMessage("*batchId*read*DataPointBinary*image*Outbound*");
        }

        [Fact]
        public void Fails_if_a_node_is_read_by_the_dataflow_but_its_own_type_only_transfers_outbound()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var image = AddChild(AddRootFolder(dataPort), DataPointBinaryDesignId, "image", typeof(byte[]), DataPortTransferMode.OnChange);
            ReadIntoDataflow(image, "image");

            var act = FluentActions.Invoking(() => DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider));

            act.Should().Throw<InvalidOperationException>().WithMessage("*image*read*DataPointBinary*Outbound*");
        }

        [Fact]
        public void Fails_if_a_node_declaring_both_directions_is_read_by_the_dataflow_under_an_ancestor_type_that_only_transfers_outbound()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var outboundFolder = AddChild(AddRootFolder(dataPort), OutboundFolderDesignId, "outbound");
            var batchId = AddChild(outboundFolder, DataPointStringDesignId, "batchId", typeof(string), DataPortTransferMode.OnChange);
            ReadIntoDataflow(batchId, "batchId");

            var act = FluentActions.Invoking(() => DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider));

            act.Should().Throw<InvalidOperationException>().WithMessage("*batchId*read*DataPointString*Outbound*");
        }

        [Fact]
        public void Deploys_a_node_declaring_both_directions_written_from_the_dataflow_under_an_ancestor_type_that_only_transfers_outbound()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var outboundFolder = AddChild(AddRootFolder(dataPort), OutboundFolderDesignId, "outbound");
            var batchId = AddChild(outboundFolder, DataPointStringDesignId, "batchId", typeof(string), DataPortTransferMode.OnChange);
            WriteFromDataflow(batchId, "batchId");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.OutgoingLinks.Should().ContainSingle();
        }

        [Fact]
        public void Fails_if_a_child_whose_type_only_transfers_outbound_is_read_by_the_dataflow()
        {
            var dataPort = CreateDataPort(DataPortDirection.In);
            var pressure = AddChild(AddRootFolder(dataPort), DataPointFloatDesignId, "pressure", typeof(double), DataPortTransferMode.OnChange);
            var engineCycle = AddChild(pressure, EngineCycleDesignId, "cycle", typeof(long));
            ReadIntoDataflow(pressure, "pressure");
            ReadIntoDataflow(engineCycle, "cycle");

            var act = FluentActions.Invoking(() => DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider));

            act.Should().Throw<InvalidOperationException>().WithMessage("*cycle*read*EngineCycle*Outbound*");
        }

        [Fact]
        public void Fails_if_a_child_that_may_never_be_linked_is_written_from_the_dataflow()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var pressure = AddChild(AddRootFolder(dataPort), DataPointFloatDesignId, "pressure", typeof(double), DataPortTransferMode.OnChange);
            var engineCycle = AddChild(pressure, EngineCycleDesignId, "cycle", typeof(long));
            WriteFromDataflow(pressure, "pressure");
            WriteFromDataflow(engineCycle, "cycle");

            var act = FluentActions.Invoking(() => DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider));

            act.Should().Throw<InvalidOperationException>().WithMessage("*cycle*written*EngineCycle*may not be linked outbound*DataPointFloat*");
        }

        [Fact]
        public void Fails_if_a_node_without_declared_directions_is_written_from_the_dataflow_but_the_data_port_type_only_transfers_inbound()
        {
            _rulesetProvider.GetRuleset(Arg.Any<string>()).Returns(RulesetFactory.CreateTestRulesetMqttInboundOnly());
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var pressure = AddChild(AddRootFolder(dataPort), DataPointFloatDesignId, "pressure", typeof(double), DataPortTransferMode.OnChange);
            WriteFromDataflow(pressure, "pressure");

            var act = FluentActions.Invoking(() => DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider));

            act.Should().Throw<InvalidOperationException>().WithMessage("*pressure*written*MqttClientInstance*DataPort*Inbound*");
        }

        [Fact]
        public void Deploys_a_child_written_from_the_dataflow_if_the_parent_type_only_transfers_outbound()
        {
            var dataPort = CreateDataPort(DataPortDirection.InOut);
            var image = AddChild(AddRootFolder(dataPort), DataPointBinaryDesignId, "image", typeof(byte[]), DataPortTransferMode.OnChange);
            var batchId = AddChild(image, UserPropertyDesignId, "batchId", typeof(string));
            WriteFromDataflow(image, "image");
            WriteFromDataflow(batchId, "batchId");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.OutgoingLinks.Should().HaveCount(2);
        }

        /// <summary>
        /// A predefined envelope child - one the ruleset gives an empty LinkDirections list -
        /// travels inside its parent's message but must never be linked, no matter how the
        /// cluster reached deployment.
        /// </summary>
        [Fact]
        public void Fails_if_a_predefined_child_is_written_from_the_dataflow()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var pressure = AddChild(AddRootFolder(dataPort), DataPointFloatDesignId, "pressure", typeof(double), DataPortTransferMode.OnChange);
            var validity = AddChild(pressure, ValidityDesignId, "valid", typeof(bool));
            WriteFromDataflow(pressure, "pressure");
            WriteFromDataflow(validity, "valid");

            var act = FluentActions.Invoking(() => DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider));

            act.Should().Throw<InvalidOperationException>().WithMessage("*valid*written*Validity*may not be linked outbound*DataPointFloat*");
        }

        /// <summary>
        /// A `Timestamp` names one direction in its `LinkDirections`: inbound it publishes the
        /// timestamp the received message carries, outbound the DataPort always sends the one
        /// belonging to the parent value, so there is nothing to link against.
        /// </summary>
        [Fact]
        public void Deploys_a_child_read_into_the_dataflow_that_may_only_be_linked_inbound()
        {
            var dataPort = CreateDataPort(DataPortDirection.In);
            var pressure = AddChild(AddRootFolder(dataPort), DataPointFloatDesignId, "pressure", typeof(double), DataPortTransferMode.OnChange);
            var timestamp = AddChild(pressure, TimestampDesignId, "Timestamp", typeof(DateTime));
            ReadIntoDataflow(pressure, "pressure");
            ReadIntoDataflow(timestamp, "timestamp");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.IncomingLinks.Should().HaveCount(2);
        }

        [Fact]
        public void Fails_if_a_child_that_may_only_be_linked_inbound_is_written_from_the_dataflow()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var pressure = AddChild(AddRootFolder(dataPort), DataPointFloatDesignId, "pressure", typeof(double), DataPortTransferMode.OnChange);
            var timestamp = AddChild(pressure, TimestampDesignId, "Timestamp", typeof(DateTime));
            WriteFromDataflow(pressure, "pressure");
            WriteFromDataflow(timestamp, "timestamp");

            var act = FluentActions.Invoking(() => DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider));

            act.Should().Throw<InvalidOperationException>().WithMessage("*Timestamp*written*may not be linked outbound*DataPointFloat*");
        }

        [Fact]
        public void Deploys_a_linkable_child_written_from_the_dataflow()
        {
            var dataPort = CreateDataPort(DataPortDirection.Out);
            var pressure = AddChild(AddRootFolder(dataPort), DataPointFloatDesignId, "pressure", typeof(double), DataPortTransferMode.OnChange);
            var batchId = AddChild(pressure, UserPropertyDesignId, "batchId", typeof(string));
            WriteFromDataflow(pressure, "pressure");
            WriteFromDataflow(batchId, "batchId");

            var transformation = DataPortTransformer.TransformToFunctionBlock(dataPort, _builder, _rulesetProvider);

            transformation.OutgoingLinks.Should().HaveCount(2);
        }

        private DataPort CreateDataPort(DataPortDirection direction)
        {
            var engine = _builder.CreateTopologyToEngine("engine");
            var dataPort = _builder.Editors.Dataflow.AddDataPort(_builder.Cluster.Dataflows.First(), Constants.MqttDataPortDesignId, "dataPort", direction, Constants.MqttRuleSetId);
            _builder.Editors.DataPort.AssignEngine(engine, dataPort);
            return dataPort;
        }

        private (DataPortTreeNode Pressure, DataPortTreeNode Timestamp, DataPortTreeNode BatchId) AddPressureWithChildren(
            DataPort dataPort, DataPortTransferMode transferMode, uint? transferIntervalInMs = default)
        {
            var pressure = AddChild(AddRootFolder(dataPort), DataPointFloatDesignId, "pressure", typeof(double), transferMode);
            if (transferIntervalInMs.HasValue)
                _builder.Editors.DataPortTreeNode.SetTransferIntervalInMs(pressure, transferIntervalInMs.Value);
            var timestamp = AddChild(pressure, UserPropertyDesignId, "timestamp", typeof(DateTime));
            var batchId = AddChild(pressure, UserPropertyDesignId, "batchId", typeof(string));
            return (pressure, timestamp, batchId);
        }

        private DataPortTreeNode AddRootFolder(DataPort dataPort)
            => _builder.Editors.DataPort.AddTreeNode(FolderDesignId, dataPort, "Folder", null, DataPortTransferMode.None);

        private DataPortTreeNode AddChild(DataPortTreeNode parent, string designId, string name, Type? valueType = default,
            DataPortTransferMode transferMode = DataPortTransferMode.None)
            => _builder.Editors.DataPortTreeNode.AddTreeNode(designId, parent, name, valueType, transferMode);

        private void WriteFromDataflow(DataPortTreeNode node, string outputName)
            => _builder.Editors.DataPortTreeNode.AssignConnector(node, _dataflowFunctionBlock.ProcessDataOutputs.Single(o => o.Name == outputName));

        private void ReadIntoDataflow(DataPortTreeNode node, string inputName)
            => _builder.Editors.DataPortTreeNode.AssignConnector(node, _dataflowFunctionBlock.ProcessDataInputs.Single(i => i.Name == inputName));

        private static Dictionary<Guid, ITransferMode> GetTransferModes(DataPortTransformation transformation)
            => transformation.FunctionBlock.Settings.Single(s => s.DesignId == DataPortTransformer.TransferModesSettingId).Value
                .Should().BeOfType<Dictionary<Guid, ITransferMode>>().Subject;
    }
}

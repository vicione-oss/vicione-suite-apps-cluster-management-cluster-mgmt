using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using ClusterManagement.Backend.DeployCluster;
using ClusterManagement.Backend.Engines;
using ClusterManagement.Public.Designs;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Extensions;
using ClusterManagement.Tests.Tools.Factories;
using Microsoft.Extensions.Logging;
using Sdk.Connections.Contracts;
using Sdk.Extensions;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;
using ViciOne.Core.Contracts.DataModel;
using ViciOne.Core.Dataflow.DataModel;
using ViciOne.Engine.DefaultPoolings;
using ViciOne.Engine.DefaultValidators;
using ViciOne.Suite.DataPort;

namespace ClusterManagement.Backend.Tests.DeployCluster;

public class ClusterToWorkflowTranslatorFacts
{
    public class CreateWorkflow
    {
        private const string SerilogFileCommunicationId = "E3601083-66AE-41FC-AC20-E05B1DD7936E";
        private const string SerilogFileCommunicationPropertyPath = "Path";
        private const string SerilogJournalCommunicationId = "96B50074-1DF9-402C-90CF-AC9894C8E243";
        private const string SerilogJournalCommunicationPropertyOutputTemplate = "OutputTemplate";
        private const string SerilogJournalCommunicationPropertyMetaDataFields = "MetaDataFields";

        [Fact]
        public void Acceptance()
        {
            var ourFunctionBlockDesign = Guid.NewGuid();
            var emptyFunctionBlockDesignId = Guid.NewGuid();
            var ourInputConnectorDesign = Guid.NewGuid();
            var ourOutputConnectorDesign = Guid.NewGuid();
            var ourSettingDesign = Guid.NewGuid();
            var ourVariableDesign = Guid.NewGuid();

            var functionBlockDesign = FunctionBlockDesignBuilder.Create(ourFunctionBlockDesign);
            functionBlockDesign.AddInput<double>(ourInputConnectorDesign);
            functionBlockDesign.AddOutput<double>(ourOutputConnectorDesign);
            functionBlockDesign.AddSetting<TimeSpan>(ourSettingDesign);
            functionBlockDesign.AddVariable<ICollection<byte>>(ourVariableDesign);
            var emptyFunctionBlockDesign = FunctionBlockDesignBuilder.Create(emptyFunctionBlockDesignId, "Empty");
            emptyFunctionBlockDesign.SystemInputs.Clear();
            emptyFunctionBlockDesign.SystemOutputs.Clear();

            var functionBlockDesignResolver = new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([functionBlockDesign, emptyFunctionBlockDesign,]));
            using ClusterBuilder builder = new(functionBlockDesignResolver);
            builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(ourFunctionBlockDesign);
            builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(emptyFunctionBlockDesignId);

            builder.Cluster.Version = new(1, 0, 204);
            var application = builder.CreateTopologyToApplication();
            var myEngineHost = builder.Editors.Application.AddEngineHost(application);
            var otherEngineHost = builder.Editors.Application.AddEngineHost(application);
            var myDataflow = builder.Cluster.Dataflows.First();

            var myEngine = builder.Editors.EngineHost.AddEngine(myEngineHost, "myEngine");
            builder.Editors.Engine.SetRunIndex(myEngine, 1);
            builder.Editors.Engine.SetLogLevel(myEngine, LogLevel.Debug);
            builder.Editors.Engine.SetMinCycleTime(myEngine, 101);

            var otherEngineOnSameHost = builder.Editors.EngineHost.AddEngine(myEngineHost, "otherEngineOnSameHost");
            builder.Editors.Engine.SetRunIndex(otherEngineOnSameHost, 2);
            builder.Editors.Engine.SetMinCycleTime(otherEngineOnSameHost, 102);

            var otherEngineOnOtherHost = builder.Editors.EngineHost.AddEngine(otherEngineHost, "otherEngineOnOtherHost");
            builder.Editors.Engine.SetRunIndex(otherEngineOnOtherHost, 2);
            builder.Editors.Engine.SetMinCycleTime(otherEngineOnOtherHost, 102);

            var otherContainer = builder.Editors.Container.AddContainer(myDataflow.Root, "otherContainer");
            var myContainer = builder.Editors.Container.AddContainer(otherContainer, "myContainer");
            var myFunctionBlock = builder.Editors.Container.AddFunctionBlock(myContainer, ourFunctionBlockDesign, "myFunctionBlock");
            builder.Editors.FunctionBlock.SetCycleFrequency(myFunctionBlock, 4);
            builder.Editors.FunctionBlock.AssignEngine(myEngine, myFunctionBlock);
            builder.Editors.FunctionBlock.SetRunMode(myFunctionBlock, FunctionBlockRunMode.Cyclic);
            var mySisterFunctionBlock = builder.Editors.Container.AddFunctionBlock(myContainer, emptyFunctionBlockDesignId, "mySisterFunctionBlock");
            builder.Editors.FunctionBlock.AssignEngine(myEngine, mySisterFunctionBlock);
            mySisterFunctionBlock.RunIndex = 3;

            var myInputSystemConnector = myFunctionBlock.SystemInputs.Single();
            var myOutputSystemConnector = myFunctionBlock.SystemOutputs.Single();
            var myInputConnector = myFunctionBlock.ProcessDataInputs.Single();
            builder.Editors.Connector.SetMarkAsChangedOnlyIfNotEqual(myInputConnector, true);
            builder.Editors.Connector.SetCrossSourcePoolingStrategy(myInputConnector, PoolingStrategy.Aggregate, out _, new MinPooling<double>());
            builder.Editors.Connector.SetPerSourcePoolingStrategy(myInputConnector, PoolingStrategy.Aggregate, out _, new MinPooling<double>());
            builder.Editors.Connector.SetValue(myInputConnector, 1.0);

            var myOutputConnector = myFunctionBlock.ProcessDataOutputs.Single();
            builder.Editors.Connector.SetMarkAsChangedOnlyIfNotEqual(myOutputConnector, false);
            builder.Editors.Connector.AddValidator(myOutputConnector, new RangeValidator<double>());

            var linkFromMeToMyself = builder.Editors.Connector.AddLink(myOutputConnector, myInputConnector); // An eigene Engine senden
            builder.Editors.Link.SetTypeConversion(linkFromMeToMyself, new TypeConversion() { TypeConverter = new DoubleConverter(), });

            var mySetting = myFunctionBlock.Settings.First(x => x.DesignId == ourSettingDesign);
            builder.Editors.Setting.SetDefaultValue(mySetting, new TimeSpan(12, 34, 56));
            builder.Editors.Setting.SetValue(mySetting, new TimeSpan(21, 01, 23));
            mySetting.ValueChangedTime = new DateTime(2022, 5, 18, 15, 41, 56, DateTimeKind.Utc);

            var myVariable = myFunctionBlock.PersistentVariables.First(x => x.DesignId == ourVariableDesign);
            builder.Editors.PersistentVariable.SetDefaultValue(myVariable, "\n"u8.ToArray());
            builder.Editors.PersistentVariable.SetValue(myVariable, "\n"u8.ToArray());
            myVariable.ValueChangedTime = new DateTime(2022, 5, 18, 15, 39, 45, DateTimeKind.Utc);

            var otherFunctionBlockOnSameHost = builder.Editors.Container.AddFunctionBlock(otherContainer, ourFunctionBlockDesign, "otherFunctionBlockOnSameHost");
            builder.Editors.FunctionBlock.AssignEngine(otherEngineOnSameHost, otherFunctionBlockOnSameHost);
            builder.Editors.FunctionBlock.SetRunMode(otherFunctionBlockOnSameHost, FunctionBlockRunMode.Change);

            var otherInputConnectorOnSameHost = otherFunctionBlockOnSameHost.ProcessDataInputs.Single();
            builder.Editors.Connector.AddLink(myOutputConnector, otherInputConnectorOnSameHost); // An andere Engine auf selben Host senden
            var otherOutputConnectorOnSameHost = otherFunctionBlockOnSameHost.ProcessDataOutputs.Single();
            builder.Editors.Connector.AddLink(otherOutputConnectorOnSameHost, myInputConnector); // Von anderer Engine auf selben Host empfangen

            var otherFunctionBlockOnOtherHost = builder.Editors.Container.AddFunctionBlock(otherContainer, ourFunctionBlockDesign, "otherFunctionBlockOnOtherHost");
            builder.Editors.FunctionBlock.AssignEngine(otherEngineOnOtherHost, otherFunctionBlockOnOtherHost);

            var otherInputConnectorOnOtherHost = otherFunctionBlockOnOtherHost.ProcessDataInputs.Single();
            builder.Editors.Connector.AddLink(myOutputConnector, otherInputConnectorOnOtherHost); // An andere Engine auf anderen Host senden
            var otherOutputConnectorOnOtherHost = otherFunctionBlockOnOtherHost.ProcessDataOutputs.Single();
            var linkFromOtherToMe = builder.Editors.Connector.AddLink(otherOutputConnectorOnOtherHost, myInputConnector); // Von anderer Engine auf anderen Host empfangen
            builder.Editors.Link.SetTypeConversion(linkFromOtherToMe, new TypeConversion() { TypeConverter = new DoubleConverter(), });

            MqttConnection brokerConnection = new()
            {
                Protocol = MqttConnectionType.TCP,
                Address = "localhost",
                Port = 1883,
                Username = "admin",
                Password = "789#+",
            };
            var dataPortNodeId = Guid.NewGuid();
            Dictionary<ViciOne.Cluster.Model.Engine, List<DataPortTransformation>> enginesDataPorts = new()
            {
                [myEngine] =
                [
                    new()
                    {
                        CommunicationId = "negligible",
                        ConnectionName = "DataExchange",
                        IncomingNodes = [],
                        IncomingLinks =
                        [
                            new() // Von DataPort empfangen
                            {
                                ConnectorId = myInputConnector.Id,
                                RemoteId = dataPortNodeId,
                                SourceType = typeof(double).AssemblyQualifiedName!,
                            },
                        ],
                        OutgoingNodes = [],
                        OutgoingLinks =
                        [
                            new() // An DataPort senden
                            {
                                ConnectorId = myOutputConnector.Id,
                                RemoteId = myOutputConnector.Id,
                            },
                        ],
                        AdditionalConnectorDesigns =
                        [
                            new()
                            {
                                ConnectorId = myInputSystemConnector.Id,
                                IsInput = true,
                                Name = "AdditionalInput",
                                ValueType = typeof(string),
                            },
                        ],
                    }
                ],
            };

            var (workflow, metadata) = ClusterToWorkflowTranslator.CreateWorkflow(builder.Cluster, myEngineHost, myEngine, functionBlockDesignResolver,
                brokerConnection, enginesDataPorts, "ThisIsMyPath");

            var clusterDependency = functionBlockDesignResolver.ResolveFunctionBlockDesignDependency(functionBlockDesign.Id);
            Backend.Engines.ClusterDependency engineClusterDependency = new(clusterDependency.Name, clusterDependency.Version.ToString());
            workflow.Should().BeEquivalentTo(new Workflow()
            {
                Containers =
                [
                    new()
                    {
                        Id = myDataflow.Root.Id,
                        Name = myDataflow.Root.Name,
                    },
                    new()
                    {
                        Id = otherContainer.Id,
                        Name = otherContainer.Name,
                        ParentContainerId = myDataflow.Root.Id,
                    },
                    new()
                    {
                        Id = myContainer.Id,
                        Name = myContainer.Name,
                        ParentContainerId = otherContainer.Id,
                    },
                ],
                Engine = new()
                {
                    ChainIndex = 1,
                    CycleTime = 101,
                    Id = myEngine.Id,
                    LogLevel = LogLevel.Debug,
                    Name = myEngine.Name,
                },
                FunctionBlockConnectorLinks =
                [
                    new() // An eigene Engine senden
                    {
                        DestinationConnectorId = myInputConnector.Id,
                        SourceConnectorId = myOutputConnector.Id,
                        TypeConverter = TypedObject.CreateOptional(linkFromMeToMyself.TypeConversion),
                        Id = linkFromMeToMyself.Id,
                    },
                ],
                FunctionBlocks =
                [
                    new()
                    {
                        Connectors =
                        [
                            new()
                            {
                                CrossSourcePoolingStrategy = PoolingStrategy.Aggregate,
                                CrossSourceAggregatingPooling = TypedObject.CreateOptional(myInputConnector.CrossSourceAggregatingPooling),
                                DesignId = ourInputConnectorDesign,
                                Id = myInputConnector.Id,
                                InitialValue = new(typeof(double).AssemblyQualifiedName!, "1"),
                                MarkAsChangedOnlyIfNotEqual = true,
                                PerSourcePoolingStrategy = PoolingStrategy.Aggregate,
                                PerSourceAggregatingPooling = TypedObject.CreateOptional(myInputConnector.CrossSourceAggregatingPooling),
                            },
                            new()
                            {
                                DesignId = ourOutputConnectorDesign,
                                InitialValue = TypedObject.CreateOptional(0.0),
                                MarkAsChangedOnlyIfNotEqual = false,
                                Id = myOutputConnector.Id,
                                Validator = TypedObject.CreateOptional(myOutputConnector.Validators.FirstOrDefault()),
                            },
                            new()
                            {
                                CrossSourcePoolingStrategy = PoolingStrategy.Queue,
                                DesignId = mySetting.Input.DesignId,
                                Id = mySetting.Input.Id,
                                InitialValue = new(typeof(TimeSpan).AssemblyQualifiedName!, "\"21:01:23\""),
                                MarkAsChangedOnlyIfNotEqual = true,
                                PerSourcePoolingStrategy = PoolingStrategy.Queue,
                            },
                            new()
                            {
                                DesignId = mySetting.Output.DesignId,
                                InitialValue = new(typeof(TimeSpan).AssemblyQualifiedName!, "\"21:01:23\""),
                                MarkAsChangedOnlyIfNotEqual = true,
                                Id = mySetting.Output.Id,
                            },
                            new()
                            {
                                DesignId = myOutputSystemConnector.DesignId,
                                InitialValue = new(typeof(bool).AssemblyQualifiedName!, "true"),
                                MarkAsChangedOnlyIfNotEqual = true,
                                Id = myOutputSystemConnector.Id,
                            },
                            new()
                            {
                                CrossSourcePoolingStrategy = PoolingStrategy.Queue,
                                DesignId = myInputSystemConnector.DesignId,
                                Id = myInputSystemConnector.Id,
                                InitialValue = new(typeof(bool).AssemblyQualifiedName!, "true"),
                                MarkAsChangedOnlyIfNotEqual = true,
                                PerSourcePoolingStrategy = PoolingStrategy.Queue,
                            },
                        ],
                        ContainerId = myContainer.Id,
                        CycleFrequency = 4,
                        DesignId = ourFunctionBlockDesign,
                        Name = myFunctionBlock.Name,
                        RunIndex = 1,
                        RunMode = FunctionBlockRunMode.Cyclic,
                        Settings =
                        [
                            new()
                            {
                                DesignId = ourSettingDesign,
                                Timestamp = mySetting.ValueChangedTime,
                                Id = mySetting.Id,
                                Value = TypedObject.CreateOptional(mySetting.Value),
                            },
                        ],
                        Id = myFunctionBlock.Id,
                        Variables =
                        [
                            new()
                            {
                                DesignId = ourVariableDesign,
                                Timestamp = myVariable.ValueChangedTime,
                                Id = myVariable.Id,
                                Value = new(typeof(byte[]).AssemblyQualifiedName!, """{"$id":"1","$values":[{"$id":"2","Type":"System.Byte, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e","Value":"10"}]}"""),
                            },
                        ],
                    },
                    new()
                    {
                        ContainerId = myContainer.Id,
                        CycleFrequency = 1,
                        DesignId = emptyFunctionBlockDesignId,
                        Name = mySisterFunctionBlock.Name,
                        Id = mySisterFunctionBlock.Id,
                        RunIndex = 0,
                    },
                ],
                AdditionalConnectorDesigns =
                [
                    new()
                    {
                        ConnectorId = myInputSystemConnector.Id,
                        IsInput = true,
                        Name = "AdditionalInput",
                        ValueType = typeof(string),
                    },
                ],
                ExternalConnections =
                [
                    new()
                    {
                        Name = "Suite MQTT Broker",
                        Communication = new()
                        {
                            Id = "07AB9DE8-6216-41FE-9E94-D324C5C55F7A",
                            Properties =
                            [
                                new("Host") { Value = TypedObject.CreateMandatory("localhost"), },
                                new("Port") { Value = TypedObject.CreateMandatory(1883), },
                                new("Username") { Value = TypedObject.CreateMandatory("admin"), },
                                new("Password") { Value = TypedObject.CreateMandatory("789#+"), },
                                new("MaxPendingMessages") { Value = TypedObject.CreateMandatory(10_000), },
                                new("QualityOfService") { Value = TypedObject.CreateMandatory((short)0), },
                                new("Retain") { Value = TypedObject.CreateMandatory(true), }
                            ],
                        },
                        IncomingLinks =
                        [
                            new() // Von anderer Engine auf anderen Host empfangen
                            {
                                ConnectorId = myInputConnector.Id,
                                RemoteId = otherOutputConnectorOnOtherHost.Id,
                                SourceType = typeof(double).AssemblyQualifiedName!,
                                TypeConverter = TypedObject.CreateOptional(linkFromOtherToMe.TypeConversion),
                            },
                        ],
                        OutgoingLinks =
                        [
                            new() // An andere Engine auf anderen Host senden
                            {
                                ConnectorId = myOutputConnector.Id,
                                RemoteId = myOutputConnector.Id,
                            },
                        ],
                    },
                    new()
                    {
                        Name = "Direct between Engines",
                        Communication = new() { Id = "718EECC8-2C38-432E-823B-0E46D64FEAF1", },
                        IncomingLinks =
                        [
                            new() // Von anderer Engine auf selben Host empfangen
                            {
                                ConnectorId = myInputConnector.Id,
                                RemoteId = otherOutputConnectorOnSameHost.Id,
                                SourceType = typeof(double).AssemblyQualifiedName!,
                            },
                        ],
                        OutgoingLinks =
                        [
                            new() // An andere Engine auf selben Host senden
                            {
                                ConnectorId = myOutputConnector.Id,
                                RemoteId = myOutputConnector.Id,
                            },
                        ],
                    },
                    new()
                    {
                        Name = "DataExchange",
                        Communication = new()
                        {
                            Id = "negligible",
                            Properties =
                            [
                                new(nameof(IDataPortCommunication.Nodes)) { Value = TypedObject.CreateMandatory<IReadOnlyCollection<Node>>([]), }
                            ],
                        },
                        IncomingLinks =
                        [
                            new() // Von DataPort empfangen
                            {
                                ConnectorId = myInputConnector.Id,
                                RemoteId = dataPortNodeId,
                                SourceType = typeof(double).AssemblyQualifiedName!,
                            },
                        ],
                    },
                    new()
                    {
                        Name = "DataExchange",
                        Communication = new()
                        {
                            Id = "negligible",
                            Properties =
                            [
                                new(nameof(IDataPortCommunication.Nodes)) { Value = TypedObject.CreateMandatory<IReadOnlyCollection<Node>>([]), }
                            ],
                        },
                        OutgoingLinks =
                        [
                            new() // An DataPort senden
                            {
                                ConnectorId = myOutputConnector.Id,
                                RemoteId = myOutputConnector.Id,
                            },
                        ],
                    },
                ],
                ClusterDependencies = [engineClusterDependency,],
                PersistenceCommunication = new()
                {
                    Protocol = MqttProtocol.TCP,
                    Address = "localhost",
                    TcpPort = 1883,
                    Username = "admin",
                    Password = "789#+",
                    QualityOfService = 1,
                    Retain = true,
                },
                RuntimeFunctionsCommunication = new()
                {
                    Protocol = MqttProtocol.TCP,
                    Address = "localhost",
                    TcpPort = 1883,
                    Username = "admin",
                    Password = "789#+",
                    QualityOfService = 2,
                    Retain = false,
                },
                EngineLogCommunication = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? new()
                    {
                        Id = SerilogJournalCommunicationId,
                        Properties =
                        [
                            new(SerilogJournalCommunicationPropertyOutputTemplate) { Value = TypedObject.CreateMandatory($$"""{{myEngine.Name}} ({{myEngine.Id}}): [{SourceContext}] {Message:lj}{NewLine}{Exception}"""), },
                            new(SerilogJournalCommunicationPropertyMetaDataFields)
                            {
                                Value = TypedObject.CreateMandatory(new Dictionary<string, object?>()
                                {
                                    { "ENGINE_ID", myEngine.Id },
                                    { "ENGINE_NAME", myEngine.Name },
                                    { "ENGINE_HOST_NAME", myEngineHost.Name },
                                    { "ENGINE_HOST_ID", myEngineHost.Id },
                                    { "MODULE_ID", "ViciOne.Suite.ClusterManagement" },
                                }),
                            },
                        ],
                    }
                    : new()
                    {
                        Id = SerilogFileCommunicationId,
                        Properties =
                        [
                            new(SerilogFileCommunicationPropertyPath) { Value = TypedObject.CreateMandatory(Path.Combine("ThisIsMyPath", $"{myEngine.Id}.log")), },
                        ],
                    },
            });
            metadata.MqttRetainTopics.Should().HaveCount(3); // An andere Engine auf anderen Host senden + Setting + Variable
        }

        [Theory]
        [MemberData(nameof(ContainerStructures))]
        [SuppressMessage("Usage", "xUnit1044:Avoid using TheoryData type arguments that are not serializable", Justification = "to complex to make it serializable")]
        public void Adds_only_container_structure_from_root_to_own_FunctionBlocks(ContainerInfo containerInfo)
        {
            var functionBlockDesignId = Guid.NewGuid();
            var functionBlockDesign = FunctionBlockDesignBuilder.Create(functionBlockDesignId);
            var functionBlockDesignResolver = new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([functionBlockDesign,]));
            using ClusterBuilder builder = new(functionBlockDesignResolver);
            builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(functionBlockDesignId);
            var engineHost = builder.CreateTopologyToEngineHost();
            var engine = builder.Editors.EngineHost.AddEngine(engineHost, "engine");

            BuildContainerStructure(containerInfo);

            void BuildContainerStructure(ContainerInfo containerInfo, ViciOne.Cluster.Model.Container? parent = default)
            {
                var container = builder.Cluster.Dataflows.First().Root;
                if (parent == null)
                    container.Id = containerInfo.Id;
                else
                    container = builder.Editors.Container.AddContainer(id: containerInfo.Id, parent: parent);
                if (containerInfo.FunctionBlock.HasValue)
                {
                    var functionBlock = builder.Editors.Container.AddFunctionBlock(container, functionBlockDesignId, id: containerInfo.FunctionBlock.Value);
                    builder.Editors.FunctionBlock.AssignEngine(engine, functionBlock);
                }
                foreach (var subContainerInfo in containerInfo.Containers)
                    BuildContainerStructure(subContainerInfo, container);
            }

            var (workflow, metadata) = ClusterToWorkflowTranslator.CreateWorkflow(builder.Cluster, engineHost, engine, functionBlockDesignResolver,
                new(), new Dictionary<ViciOne.Cluster.Model.Engine, List<DataPortTransformation>>(), string.Empty);

            var containerInfos = containerInfo.Flatten(x => x.Containers).ToList();
            workflow.Containers.Count.Should().Be(containerInfos.Count(x => x.ShouldExists));
            workflow.FunctionBlocks.Count.Should().Be(containerInfos.Count(x => x.FunctionBlock.HasValue));

            foreach (var (container, parentContainer) in containerInfo.TraverseBreadthFirstWithParent(x => x.Containers))
            {
                if (container.ShouldExists)
                {
                    workflow.Containers.Should().ContainSingle(x => x.Id == container.Id)
                        .Which.ParentContainerId.Should().Be(parentContainer?.Id);
                }
                else
                {
                    workflow.Containers.Should().NotContain(x => x.Id == container.Id);
                }
                if (container.FunctionBlock.HasValue)
                {
                    workflow.FunctionBlocks.Should().ContainSingle(x => x.Id == container.FunctionBlock.Value)
                        .Which.ContainerId.Should().Be(container.Id);
                }
            }
        }

        public static TheoryData<ContainerInfo> ContainerStructures()
            => new()
            {
                {
                    new ContainerInfo()
                    {
                        FunctionBlock = Guid.NewGuid(),
                    }
                },
                {
                    new ContainerInfo()
                    {
                        Containers =
                        [
                            new()
                            {
                                Containers =
                                [
                                    new()
                                    {
                                        FunctionBlock = Guid.NewGuid(),
                                    },
                                ],
                            },
                        ],
                    }
                },
                {
                    new ContainerInfo()
                    {
                        Containers =
                        [
                            new()
                            {
                                FunctionBlock = Guid.NewGuid(),
                                Containers =
                                [
                                    new()
                                    {
                                        ShouldExists = false,
                                    },
                                ],
                            },
                        ],
                    }
                },
                {
                    new ContainerInfo()
                    {
                        FunctionBlock = Guid.NewGuid(),
                        Containers =
                        [
                            new()
                            {
                                ShouldExists = false,
                                Containers =
                                [
                                    new()
                                    {
                                        ShouldExists = false,
                                    },
                                ],
                            },
                        ],
                    }
                },
                {
                    new ContainerInfo()
                    {
                        ShouldExists = false,
                        Containers =
                        [
                            new()
                            {
                                ShouldExists = false,
                                Containers =
                                [
                                    new()
                                    {
                                        ShouldExists = false,
                                    },
                                ],
                            },
                        ],
                    }
                },
                {
                    new ContainerInfo()
                    {
                        FunctionBlock = Guid.NewGuid(),
                        Containers =
                        [
                            new()
                            {
                                Containers =
                                [
                                    new()
                                    {
                                        FunctionBlock = Guid.NewGuid(),
                                    },
                                ],
                            },
                        ],
                    }
                },
                {
                    new ContainerInfo()
                    {
                        FunctionBlock = Guid.NewGuid(),
                        Containers =
                        [
                            new()
                            {
                                FunctionBlock = Guid.NewGuid(),
                                Containers =
                                [
                                    new()
                                    {
                                        ShouldExists = false,
                                    },
                                ],
                            },
                        ],
                    }
                },
                {
                    new ContainerInfo()
                    {
                        Containers =
                        [
                            new()
                            {
                                Containers =
                                [
                                    new()
                                    {
                                        Containers =
                                        [
                                            new()
                                            {
                                                FunctionBlock = Guid.NewGuid(),
                                            },
                                            new()
                                            {
                                                FunctionBlock = Guid.NewGuid(),
                                            },
                                        ],
                                    },
                                    new()
                                    {
                                        ShouldExists = false,
                                    },
                                ],
                            },
                            new()
                            {
                                ShouldExists = false,
                            },
                        ],
                    }
                },
            };

        public sealed class ContainerInfo
        {
            public Guid Id { get; } = Guid.NewGuid();
            public List<ContainerInfo> Containers { get; init; } = [];
            public Guid? FunctionBlock { get; init; }
            public bool ShouldExists { get; init; } = true;
        }

        [Fact]
        public void Supports_several_dataflows()
        {
            var functionBlockDesignId = Guid.NewGuid();
            var functionBlockDesign = FunctionBlockDesignBuilder.Create(functionBlockDesignId);
            var functionBlockDesignResolver = new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([functionBlockDesign,]));
            using ClusterBuilder builder = new(functionBlockDesignResolver);
            builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(functionBlockDesignId);

            var engineHost = builder.CreateTopologyToEngineHost();
            var engine1 = builder.Editors.EngineHost.AddEngine(engineHost, "engine1");
            var dataflow1 = builder.Cluster.Dataflows.First();
            var functionBlock1 = builder.Editors.Container.AddFunctionBlock(dataflow1.Root, functionBlockDesignId, "FunctionBlock1");
            builder.Editors.FunctionBlock.AssignEngine(engine1, functionBlock1);
            var engine2 = builder.Editors.EngineHost.AddEngine(engineHost, "engine1");
            var dataflow2 = builder.Editors.Cluster.AddDataflow(dataflow1.Name, dataflow1.Version);
            var functionBlock2 = builder.Editors.Container.AddFunctionBlock(dataflow2.Root, functionBlockDesignId, "FunctionBlock2");
            builder.Editors.FunctionBlock.AssignEngine(engine2, functionBlock2);

            var (workflow, metadata) = ClusterToWorkflowTranslator.CreateWorkflow(builder.Cluster, engineHost, engine1, functionBlockDesignResolver,
                new(), new Dictionary<ViciOne.Cluster.Model.Engine, List<DataPortTransformation>>(), string.Empty);

            workflow.Engine.Id.Should().Be(engine1.Id);
            workflow.Containers.Should().BeEquivalentTo(new Backend.Engines.Container[]
            {
                new()
                {
                    Id = dataflow1.Root.Id,
                    Name = dataflow1.Root.Name,
                },
            });
            var actualFunctionBlock = workflow.FunctionBlocks.Should().ContainSingle().Subject;
            actualFunctionBlock.Id.Should().Be(functionBlock1.Id);
            actualFunctionBlock.ContainerId.Should().Be(dataflow1.Root.Id);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void Sends_connector_value_to_broker_only_once_if_it_is_requested_multiple_times(bool eventEnabled, bool linkToRemoteEngine)
        {
            var functionBlockDesignId = Guid.NewGuid();
            var functionBlockDesign = FunctionBlockDesignBuilder.Create(functionBlockDesignId);
            var functionBlockDesignResolver = new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([functionBlockDesign,]));
            using ClusterBuilder builder = new(functionBlockDesignResolver);
            builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(functionBlockDesignId);

            var application = builder.CreateTopologyToApplication();
            var myEngineHost = builder.Editors.Application.AddEngineHost(application);
            var otherEngineHost = builder.Editors.Application.AddEngineHost(application);
            var myEngine = builder.Editors.EngineHost.AddEngine(myEngineHost, "myEngine");
            var remoteEngine = builder.Editors.EngineHost.AddEngine(otherEngineHost, "remoteEngine");

            var dataflow = builder.Cluster.Dataflows.First();
            var myFunctionBlock = builder.Editors.Container.AddFunctionBlock(dataflow.Root, functionBlockDesignId, "myFunctionBlock");
            builder.Editors.FunctionBlock.AssignEngine(myEngine, myFunctionBlock);
            var remoteFunctionBlock = builder.Editors.Container.AddFunctionBlock(dataflow.Root, functionBlockDesignId, "remoteFunctionBlock");
            builder.Editors.FunctionBlock.AssignEngine(remoteEngine, remoteFunctionBlock);

            var myOutputConnector = myFunctionBlock.SystemOutputs.Single();

            if (eventEnabled)
                builder.Editors.Connector.SetEventEnabled(true, myOutputConnector);

            if (linkToRemoteEngine)
            {
                var remoteInputConnector = remoteFunctionBlock.SystemInputs.Single();
                builder.Editors.Connector.AddLink(myOutputConnector, remoteInputConnector);
            }

            // act
            var (workflow, metadata) = ClusterToWorkflowTranslator.CreateWorkflow(builder.Cluster, myEngineHost, myEngine,
                functionBlockDesignResolver, new(), new Dictionary<ViciOne.Cluster.Model.Engine, List<DataPortTransformation>>(), string.Empty);

            // assert
            if (eventEnabled || linkToRemoteEngine)
            {
                var externalConnection = workflow.ExternalConnections
                    .Should().Contain(c => c.Communication.Id == "07AB9DE8-6216-41FE-9E94-D324C5C55F7A").Subject;
                var outgoingLink = externalConnection.OutgoingLinks.Should().ContainSingle().Subject;
                outgoingLink.RemoteId.Should().Be(outgoingLink.ConnectorId);
                outgoingLink.ConnectorId.Should().Be(myOutputConnector.Id);
                metadata.MqttRetainTopics.Should().ContainSingle();
            }
            else
            {
                workflow.ExternalConnections.Should().BeEmpty();
                metadata.MqttRetainTopics.Should().BeEmpty();
            }
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void Receives_connector_value_from_broker_for_each_request(bool eventEnabled, bool linkFromRemoteEngine)
        {
            var functionBlockDesignId = Guid.NewGuid();
            var functionBlockDesign = FunctionBlockDesignBuilder.Create(functionBlockDesignId);
            var functionBlockDesignResolver = new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([functionBlockDesign,]));
            using ClusterBuilder builder = new(functionBlockDesignResolver);
            builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(functionBlockDesignId);

            var application = builder.CreateTopologyToApplication();
            var myEngineHost = builder.Editors.Application.AddEngineHost(application);
            var otherEngineHost = builder.Editors.Application.AddEngineHost(application);
            var myEngine = builder.Editors.EngineHost.AddEngine(myEngineHost, "myEngine");
            var remoteEngine = builder.Editors.EngineHost.AddEngine(otherEngineHost, "remoteEngine");

            var dataflow = builder.Cluster.Dataflows.First();
            var myFunctionBlock = builder.Editors.Container.AddFunctionBlock(dataflow.Root, functionBlockDesignId, "myFunctionBlock");
            builder.Editors.FunctionBlock.AssignEngine(myEngine, myFunctionBlock);
            var remoteFunctionBlock = builder.Editors.Container.AddFunctionBlock(dataflow.Root, functionBlockDesignId, "remoteFunctionBlock");
            builder.Editors.FunctionBlock.AssignEngine(remoteEngine, remoteFunctionBlock);

            var myInputConnector = myFunctionBlock.SystemInputs.Single();
            var remoteOutputConnector = remoteFunctionBlock.SystemOutputs.Single();

            if (eventEnabled)
                builder.Editors.Connector.SetEventEnabled(true, myInputConnector);

            if (linkFromRemoteEngine)
                builder.Editors.Connector.AddLink(remoteOutputConnector, myInputConnector);

            // act
            var (workflow, metadata) = ClusterToWorkflowTranslator.CreateWorkflow(builder.Cluster, myEngineHost, myEngine,
                functionBlockDesignResolver, new(), new Dictionary<ViciOne.Cluster.Model.Engine, List<DataPortTransformation>>(), string.Empty);

            // assert
            if (eventEnabled || linkFromRemoteEngine)
            {
                List<Action<IncomingLink>> elementInspectors = [];
                if (eventEnabled)
                {
                    elementInspectors.Add(link =>
                    {
                        link.ConnectorId.Should().Be(myInputConnector.Id);
                        link.RemoteId.Should().Be(myInputConnector.Id);
                        link.SourceType.Should().Be(typeof(bool).AssemblyQualifiedName);
                        link.TypeConverter.Should().BeNull();
                    });
                }
                if (linkFromRemoteEngine)
                {
                    elementInspectors.Add(link =>
                    {
                        link.ConnectorId.Should().Be(myInputConnector.Id);
                        link.RemoteId.Should().Be(remoteOutputConnector.Id);
                        link.SourceType.Should().Be(typeof(bool).AssemblyQualifiedName);
                        link.TypeConverter.Should().BeNull();
                    });
                }
                var externalConnection = workflow.ExternalConnections
                    .Should().Contain(c => c.Communication.Id == "07AB9DE8-6216-41FE-9E94-D324C5C55F7A")
                    .Which.IncomingLinks.Should().SatisfyRespectively(elementInspectors);
            }
            else
            {
                workflow.ExternalConnections.Should().BeEmpty();
            }
            metadata.MqttRetainTopics.Should().BeEmpty();
        }

        [Fact]
        public void Uses_data_type_of_additional_connector_design_if_connector_is_not_known_on_FunctionBlock_design()
        {
            var myFunctionBlockDesignId = Guid.NewGuid();
            var myInputConnectorDesignId = Guid.NewGuid();
            var myFunctionBlockDesign = FunctionBlockDesignBuilder.Create(myFunctionBlockDesignId);
            myFunctionBlockDesign.AddInput<double>(myInputConnectorDesignId);

            var dataPortFunctionBlockDesignId = Guid.NewGuid();
            var dataPortFunctionBlockDesign = FunctionBlockDesignBuilder.Create(dataPortFunctionBlockDesignId, "DataPort");
            dataPortFunctionBlockDesign.SystemInputs.Clear();
            var dataPortOutputConnectorDesign = dataPortFunctionBlockDesign.SystemOutputs.First();
            var dataPortOutputConnectorId = Guid.NewGuid();
            dataPortFunctionBlockDesign.SystemOutputs.Clear();

            var functionBlockDesignResolver = new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([myFunctionBlockDesign, dataPortFunctionBlockDesign,]));
            using ClusterBuilder builder = new(functionBlockDesignResolver);
            builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(myFunctionBlockDesignId);
            builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(dataPortFunctionBlockDesignId);

            var engineHost = builder.CreateTopologyToEngineHost();
            var dataflow = builder.Cluster.Dataflows.First();

            var myEngine = builder.Editors.EngineHost.AddEngine(engineHost, "myEngine");
            var myFunctionBlock = builder.Editors.Container.AddFunctionBlock(dataflow.Root, myFunctionBlockDesignId, "myFunctionBlock");
            builder.Editors.FunctionBlock.AssignEngine(myEngine, myFunctionBlock);
            var myInputConnector = myFunctionBlock.ProcessDataInputs.Single();

            var dataPortEngine = builder.Editors.EngineHost.AddEngine(engineHost, "dataPortEngine");
            var dataPortFunctionBlock = builder.Editors.Container.AddFunctionBlock(dataflow.Root, myFunctionBlockDesignId, "dataPortFunctionBlock");
            builder.Editors.FunctionBlock.AssignEngine(dataPortEngine, dataPortFunctionBlock);
            ConnectorOutput dataPortOutputConnector = new()
            {
                DesignId = dataPortOutputConnectorDesign.Id,
                Id = dataPortOutputConnectorId,
                Name = dataPortOutputConnectorDesign.Name,
                FunctionBlock = dataPortFunctionBlock,
            };
            dataPortFunctionBlock.Outputs.Add(dataPortOutputConnector);

            // DataPort empfängt Daten und gibt sie über Enginegrenze hinweg an unseren FunctionBlock weiter
            ViciOne.Cluster.Model.Link link = new()
            {
                Id = Guid.NewGuid(),
                DestinationConnector = myInputConnector,
                SourceConnector = dataPortOutputConnector,
                SourceConnectorId = dataPortOutputConnector.Id,
            };
            dataPortOutputConnector.Links.Add(link);
            myInputConnector.IncomingLinks.Add(link);

            Dictionary<ViciOne.Cluster.Model.Engine, List<DataPortTransformation>> enginesDataPorts = new()
            {
                [dataPortEngine] =
                [
                    new()
                    {
                        AdditionalConnectorDesigns =
                        [
                            new()
                            {
                                ConnectorId = dataPortOutputConnector.Id,
                                IsInput = false,
                                Name = "AdditionalInput",
                                ValueType = typeof(string),
                            },
                        ],
                    }
                ],
            };

            var (workflow, metadata) = ClusterToWorkflowTranslator.CreateWorkflow(builder.Cluster, engineHost,
                myEngine, functionBlockDesignResolver, new(), enginesDataPorts, string.Empty);

            workflow.ExternalConnections.Should().ContainSingle()
                .Which.IncomingLinks.Should().ContainSingle()
                .Which.SourceType.Should().Be(typeof(string).AssemblyQualifiedName!);
            metadata.MqttRetainTopics.Should().BeEmpty();
        }

        [Fact]
        public void Throws_if_data_type_of_source_connector_cannot_be_determined()
        {
            var myFunctionBlockDesignId = Guid.NewGuid();
            var myInputConnectorDesignId = Guid.NewGuid();
            var myFunctionBlockDesign = FunctionBlockDesignBuilder.Create(myFunctionBlockDesignId);
            myFunctionBlockDesign.AddInput<double>(myInputConnectorDesignId, "Value");

            var dataPortFunctionBlockDesignId = Guid.NewGuid();
            var dataPortFunctionBlockDesign = FunctionBlockDesignBuilder.Create(dataPortFunctionBlockDesignId, "DataPort");
            dataPortFunctionBlockDesign.SystemInputs.Clear();
            var dataPortOutputConnectorDesign = dataPortFunctionBlockDesign.SystemOutputs.First();
            var dataPortOutputConnectorId = Guid.NewGuid();
            dataPortFunctionBlockDesign.SystemOutputs.Clear();

            var functionBlockDesignResolver = new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([myFunctionBlockDesign, dataPortFunctionBlockDesign,]));
            using ClusterBuilder builder = new(functionBlockDesignResolver);
            builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(myFunctionBlockDesignId);
            builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(dataPortFunctionBlockDesignId);

            var engineHost = builder.CreateTopologyToEngineHost();
            var dataflow = builder.Cluster.Dataflows.First();

            var myEngine = builder.Editors.EngineHost.AddEngine(engineHost, "myEngine");
            var myFunctionBlock = builder.Editors.Container.AddFunctionBlock(dataflow.Root, myFunctionBlockDesignId, "myFunctionBlock");
            builder.Editors.FunctionBlock.AssignEngine(myEngine, myFunctionBlock);
            var myInputConnector = myFunctionBlock.ProcessDataInputs.Single();

            var dataPortEngine = builder.Editors.EngineHost.AddEngine(engineHost, "dataPortEngine");
            var dataPortFunctionBlock = builder.Editors.Container.AddFunctionBlock(dataflow.Root, myFunctionBlockDesignId, "dataPortFunctionBlock");
            builder.Editors.FunctionBlock.AssignEngine(dataPortEngine, dataPortFunctionBlock);
            ConnectorOutput dataPortOutputConnector = new()
            {
                DesignId = dataPortOutputConnectorDesign.Id,
                Id = dataPortOutputConnectorId,
                Name = dataPortOutputConnectorDesign.Name,
                FunctionBlock = dataPortFunctionBlock,
            };
            dataPortFunctionBlock.Outputs.Add(dataPortOutputConnector);

            // DataPort empfängt Daten und gibt sie über Enginegrenze hinweg an unseren FunctionBlock weiter
            ViciOne.Cluster.Model.Link link = new()
            {
                Id = Guid.NewGuid(),
                DestinationConnector = myInputConnector,
                SourceConnector = dataPortOutputConnector,
                SourceConnectorId = dataPortOutputConnector.Id,
            };
            dataPortOutputConnector.Links.Add(link);
            myInputConnector.IncomingLinks.Add(link);

            var act = FluentActions.Invoking(() => ClusterToWorkflowTranslator.CreateWorkflow(builder.Cluster, engineHost,
                myEngine, functionBlockDesignResolver, new(), new Dictionary<ViciOne.Cluster.Model.Engine, List<DataPortTransformation>>(), string.Empty));

            act.Should().Throw<InvalidOperationException>()
                .WithMessage($"*not*determine*data*type*{dataPortOutputConnector.Id}*{dataPortOutputConnector.Name}*{link.Id}*{myInputConnector.Id}*{myInputConnector.Name}*FunctionBlock*{myFunctionBlock.Id}*{myFunctionBlock.Name}*");
        }

        [Fact]
        public void Ignores_FunctionBlock_without_assigned_engine()
        {
            var functionBlockDesignId = Guid.NewGuid();
            var functionBlockDesign = FunctionBlockDesignBuilder.Create(functionBlockDesignId);
            var functionBlockDesignResolver = new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([functionBlockDesign,]));
            using ClusterBuilder builder = new(functionBlockDesignResolver);
            builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(functionBlockDesignId);
            var engineHost = builder.CreateTopologyToEngineHost();
            var engine = builder.Editors.EngineHost.AddEngine(engineHost, "engine");

            var dataflow = builder.Cluster.Dataflows.First();
            var functionBlock = builder.Editors.Container.AddFunctionBlock(dataflow.Root, functionBlockDesignId);

            var (workflow, metadata) = ClusterToWorkflowTranslator.CreateWorkflow(builder.Cluster, engineHost, engine,
                functionBlockDesignResolver, new(), new Dictionary<ViciOne.Cluster.Model.Engine, List<DataPortTransformation>>(), string.Empty);

            workflow.Containers.Should().BeEmpty();
            workflow.FunctionBlocks.Should().BeEmpty();
            metadata.MqttRetainTopics.Should().BeEmpty();
        }

        [Fact]
        public void Ignores_links_from_DataPortTreeNode()
        {
            var functionBlockDesignId = Guid.NewGuid();
            var inputConnectorDesignId = Guid.NewGuid();
            var outputConnectorDesignId = Guid.NewGuid();
            var functionBlockDesign = FunctionBlockDesignBuilder.Create(functionBlockDesignId);
            functionBlockDesign.AddInput<double>(inputConnectorDesignId);
            functionBlockDesign.AddOutput<double>(outputConnectorDesignId);

            var functionBlockDesignResolver = new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([functionBlockDesign,]));
            using ClusterBuilder builder = new(functionBlockDesignResolver);
            builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(functionBlockDesignId);

            var engineHost = builder.CreateTopologyToEngineHost();
            var dataflow = builder.Cluster.Dataflows.First();
            var engine = builder.Editors.EngineHost.AddEngine(engineHost, "engine");
            var functionBlock = builder.Editors.Container.AddFunctionBlock(dataflow.Root, functionBlockDesignId, "functionBlock");
            builder.Editors.FunctionBlock.AssignEngine(engine, functionBlock);

            var link = builder.Editors.Connector.AddLink(functionBlock.ProcessDataOutputs.First(), functionBlock.ProcessDataInputs.First());
            link.SourceDataPortTreeNode = new() { Id = Guid.NewGuid(), };
            link.SourceDataPortTreeNodeId = link.SourceDataPortTreeNode.Id;
            link.SourceConnector = default;
            link.SourceConnectorId = default;

            var (workflow, metadata) = ClusterToWorkflowTranslator.CreateWorkflow(builder.Cluster, engineHost,
                engine, functionBlockDesignResolver, new(), new Dictionary<ViciOne.Cluster.Model.Engine, List<DataPortTransformation>>(), string.Empty);

            workflow.FunctionBlockConnectorLinks.Should().BeEmpty();
            workflow.ExternalConnections.Should().BeEmpty();
            metadata.MqttRetainTopics.Should().BeEmpty();
        }

        [Fact]
        public void Throws_if_source_connector_of_link_is_null()
        {
            var functionBlockDesignId = Guid.NewGuid();
            var inputConnectorDesignId = Guid.NewGuid();
            var outputConnectorDesignId = Guid.NewGuid();
            var functionBlockDesign = FunctionBlockDesignBuilder.Create(functionBlockDesignId);
            functionBlockDesign.AddInput<double>(inputConnectorDesignId);
            functionBlockDesign.AddOutput<double>(outputConnectorDesignId);

            var functionBlockDesignResolver = new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([functionBlockDesign,]));
            using ClusterBuilder builder = new(functionBlockDesignResolver);
            builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(functionBlockDesignId);

            var engineHost = builder.CreateTopologyToEngineHost();
            var dataflow = builder.Cluster.Dataflows.First();
            var engine = builder.Editors.EngineHost.AddEngine(engineHost, "engine");
            var functionBlock = builder.Editors.Container.AddFunctionBlock(dataflow.Root, functionBlockDesignId, "functionBlock");
            builder.Editors.FunctionBlock.AssignEngine(engine, functionBlock);
            var inputConnector = functionBlock.ProcessDataInputs.First();

            var link = builder.Editors.Connector.AddLink(functionBlock.ProcessDataOutputs.First(), inputConnector);
            link.SourceConnector = default;
            link.SourceConnectorId = default;

            var act = FluentActions.Invoking(() => ClusterToWorkflowTranslator.CreateWorkflow(builder.Cluster, engineHost,
                engine, functionBlockDesignResolver, new(), new Dictionary<ViciOne.Cluster.Model.Engine, List<DataPortTransformation>>(), string.Empty));

            act.Should().Throw<InvalidOperationException>()
                .WithMessage($"*not*determine*source*{link.Id}*{inputConnector.Id}*{inputConnector.Name}*FunctionBlock*{functionBlock.Id}*{functionBlock.Name}*");
        }
    }
}

using System.ComponentModel;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using ClusterManagement.Backend.Engines;
using ClusterManagement.Backend.Tests.TestUtils;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ClusterManagement.Backend.Tests.Engines;

public class WorkflowTranslatorFacts
{
    public class ToStartParameter
    {
        private const string MqttCommunicationId = "07AB9DE8-6216-41FE-9E94-D324C5C55F7A";
        private const string MqttCommunicationPropertyHost = "Host";
        private const string MqttCommunicationPropertyPort = "Port";
        private const string MqttCommunicationPropertyUsername = "Username";
        private const string MqttCmmunicationPropertyPassword = "Password";
        private const string MqttCommunicationPropertyUri = "Uri";
        private const string MqttCommunicationPropertyMaxPendingMessages = "MaxPendingMessages";
        private const string MqttCommunicationPropertyQualityOfService = "QualityOfService";
        private const string MqttCommunicationPropertyRetain = "Retain";
        private const string FileSystemCommunicationId = "8B58C1F9-D120-42E9-8F63-B7C62FE1C415";
        private const string FileSystemCommunicationPropertyDirectory = "Directory";

        [Fact]
        public void Acceptance()
        {
            var engineId = Guid.NewGuid();
            Backend.Engines.Container container1 = new()
            {
                Id = Guid.Parse("00000000-1000-0000-0000-000000000000"),
                Name = "Container1",
            };
            Backend.Engines.Container container2 = new()
            {
                Id = Guid.Parse("00000000-2000-0000-0000-000000000000"),
                Name = "Container2",
                ParentContainerId = container1.Id,
            };
            FunctionBlockConnector inputConnector = new()
            {
                CrossSourcePoolingStrategy = ViciOne.Core.Contracts.DataModel.PoolingStrategy.Aggregate,
                CrossSourceAggregatingPooling = TypedObject.CreateOptional(new ViciOne.Engine.DefaultPoolings.OrPooling()),
                DesignId = Guid.Parse("10000000-1111-0000-0000-000000000001"),
                Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                InitialValue = TypedObject.CreateOptional(1000),
                MarkAsChangedOnlyIfNotEqual = true,
                PerSourcePoolingStrategy = ViciOne.Core.Contracts.DataModel.PoolingStrategy.Aggregate,
                PerSourceAggregatingPooling = TypedObject.CreateOptional(new ViciOne.Engine.DefaultPoolings.AndPooling()),
                Validator = TypedObject.CreateOptional(new ViciOne.Engine.DefaultValidators.StringLengthValidator()),
            };
            FunctionBlockConnector outputConnector = new()
            {
                CrossSourcePoolingStrategy = ViciOne.Core.Contracts.DataModel.PoolingStrategy.Concat,
                DesignId = Guid.Parse("10000000-1111-0000-0000-000000000002"),
                Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
                InitialValue = TypedObject.CreateOptional(10.23),
                MarkAsChangedOnlyIfNotEqual = true,
                PerSourcePoolingStrategy = ViciOne.Core.Contracts.DataModel.PoolingStrategy.Concat,
            };
            FunctionBlockConnector inputConnectorSetting = new()
            {
                DesignId = Guid.Parse("10000000-1111-0000-0000-000000000003"),
                Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
                InitialValue = new("my-type", """{"Value":500}"""),
            };
            FunctionBlock functionBlock1 = new()
            {
                DesignId = Guid.Parse("10000000-1111-0000-0000-000000000000"),
                Id = Guid.Parse("10000000-0000-0000-0000-000000000000"),
                Name = "test fb",
                RunMode = ViciOne.Cluster.Model.FunctionBlockRunMode.Cyclic,
                CycleFrequency = 10,
                ContainerId = container2.Id,
                Connectors =
                [
                    inputConnector,
                    outputConnector,
                    inputConnectorSetting,
                ],
                RunIndex = 3,
                Settings =
                [
                    new()
                    {
                        DesignId = Guid.Parse("10000000-1111-1111-0000-000000000001"),
                        Id = Guid.Parse("10000000-0000-1111-0000-000000000001"),
                        Timestamp = new DateTime(2021, 08, 23),
                        Value = new("my-type", """{"Value": 500}"""),
                    },
                ],
                Variables =
                [
                    new()
                    {
                        DesignId = Guid.Parse("10000000-1111-0000-1111-000000000001"),
                        Id = Guid.Parse("10000000-0000-0000-1111-000000000001"),
                        Timestamp = new DateTime(2021, 08, 24),
                        Value = TypedObject.CreateOptional(true),
                    },
                ],
            };
            FunctionBlock functionBlock2 = new()
            {
                DesignId = Guid.Parse("10000000-1111-0000-0000-000000000000"),
                Id = Guid.Parse("20000000-0000-0000-0000-000000000000"),
                ContainerId = container2.Id,
                RunIndex = 1,
            };
            var outputConnectorOfOtherEngine = Guid.NewGuid();
            var dataPortNode = Guid.NewGuid();
            CommunicationConfiguration communicationRemote = new()
            {
                Id = MqttCommunicationId,
                Properties =
                [
                    new(MqttCommunicationPropertyUri) { Value = TypedObject.CreateMandatory(new Uri("ws://remote/data")), },
                    new(MqttCommunicationPropertyUsername) { Value = TypedObject.CreateMandatory("user"), },
                    new(MqttCmmunicationPropertyPassword) { Value = TypedObject.CreateMandatory("123-."), },
                    new(MqttCommunicationPropertyMaxPendingMessages) { Value = TypedObject.CreateMandatory(5_000), },
                    new(MqttCommunicationPropertyQualityOfService) { Value = TypedObject.CreateMandatory((short)1), },
                    new(MqttCommunicationPropertyRetain) { Value = TypedObject.CreateMandatory(true), },
                ],
            };
            CommunicationConfiguration communicationMesh = new()
            {
                Id = MqttCommunicationId,
                Properties =
                [
                    new(MqttCommunicationPropertyHost) { Value = TypedObject.CreateMandatory("mesh"), },
                    new(MqttCommunicationPropertyPort) { Value = TypedObject.CreateMandatory(9000), },
                    new(MqttCommunicationPropertyUsername) { Value = TypedObject.CreateMandatory("admin"), },
                    new(MqttCmmunicationPropertyPassword) { Value = TypedObject.CreateMandatory("789#"), },
                    new(MqttCommunicationPropertyMaxPendingMessages) { Value = TypedObject.CreateMandatory(10_000), },
                    new(MqttCommunicationPropertyQualityOfService) { Value = TypedObject.CreateMandatory((short)2), },
                    new(MqttCommunicationPropertyRetain) { Value = TypedObject.CreateMandatory(true), },
                ],
            };
            CommunicationConfiguration communicationLocal = new()
            {
                Id = MqttCommunicationId,
                Properties =
                [
                    new(MqttCommunicationPropertyHost) { Value = TypedObject.CreateMandatory("local"), },
                    new(MqttCommunicationPropertyPort) { Value = TypedObject.CreateMandatory(1883), },
                ],
            };
            MqttCommunication communicationPersistence = new()
            {
                Protocol = MqttProtocol.TCP,
                Address = "local",
                TcpPort = 1883,
                QualityOfService = 1,
                Retain = true,
            };
            MqttCommunication communicationRuntimeFunctions = new()
            {
                Protocol = MqttProtocol.TCP,
                Address = "local",
                TcpPort = 1883,
                QualityOfService = 2,
                Retain = false,
            };
            const string localText = "Lokal";
            const string meshText = "Mesh";
            const string remoteText = "Remote";
            Workflow workflow = new()
            {
                Engine = new()
                {
                    Id = engineId,
                    Name = "Factory",
                    ChainIndex = 1,
                    CycleTime = 10,
                    LogLevel = LogLevel.Debug,
                },
                Containers =
                [
                    container1,
                    container2,
                ],
                FunctionBlocks =
                [
                    functionBlock1,
                    functionBlock2,
                ],
                FunctionBlockConnectorLinks =
                [
                    new() // Empfang von eigener Engine
                    {
                        Id = Guid.Parse("10000000-2222-0000-0000-000000000001"),
                        SourceConnectorId = outputConnector.Id,
                        DestinationConnectorId = inputConnector.Id,
                        TypeConverter = TypedObject.CreateOptional(new DoubleConverter()),
                    },
                ],
                AdditionalConnectorDesigns =
                [
                    new()
                    {
                        ConnectorId = inputConnector.Id,
                        IsInput = true,
                        Name = "AdditionalInput",
                        ValueType = typeof(int),
                    },
                ],
                ExternalConnections =
                [
                    new()
                    {
                        Name = remoteText,
                        Communication = communicationRemote,
                        IncomingLinks =
                        [
                            new() // Empfang von anderer Engine
                            {
                                ConnectorId = inputConnector.Id,
                                RemoteId = outputConnectorOfOtherEngine,
                                SourceType = typeof(double).AssemblyQualifiedName!,
                                TypeConverter = TypedObject.CreateOptional(new DoubleConverter()),
                            },
                        ],
                    },
                    new()
                    {
                        Name = meshText,
                        Communication = communicationMesh,
                        IncomingLinks =
                        [
                            new() // Empfang von DataPort
                            {
                                ConnectorId = inputConnector.Id,
                                RemoteId = dataPortNode,
                                SourceType = typeof(int).AssemblyQualifiedName!,
                            },
                        ],
                        OutgoingLinks =
                        [
                            new() // Protokollierung von Prozessdaten
                            {
                                ConnectorId = inputConnector.Id,
                                RemoteId = inputConnector.Id,
                            },
                        ],
                    },
                    new()
                    {
                        Name = localText,
                        Communication = communicationLocal,
                        OutgoingLinks =
                        [
                            new() // Senden an andere Engine
                            {
                                ConnectorId = outputConnector.Id,
                                RemoteId = outputConnector.Id,
                            },
                            new() // Senden an DataPort
                            {
                                ConnectorId = outputConnector.Id,
                                RemoteId = dataPortNode,
                            },
                        ],
                    },
                ],
                PersistenceCommunication = communicationPersistence,
                RuntimeFunctionsCommunication = communicationRuntimeFunctions,
                EngineLogCommunication = new()
                {
                    Id = "engine-log-communication",
                    Properties =
                    [
                        new("engine-log-communication-property") { Value = TypedObject.CreateMandatory("engine-log-communication-property-value"), },
                    ],
                },
            };

            var startParameter = workflow.ToStartParameter();

            startParameter.Engine.Should().BeEquivalentTo(new Backend.Engines.CommunicationContracts.Engine()
            {
                UniqueIdentifier = engineId.ToString(),
                Name = "Factory",
            });
            Backend.Engines.CommunicationContracts.Container expectedContainer1 = new()
            {
                UniqueIdentifier = container1.Id.ToString(),
                Name = container1.Name,
            };
            Backend.Engines.CommunicationContracts.Container expectedContainer2 = new()
            {
                UniqueIdentifier = container2.Id.ToString(),
                Name = container2.Name,
                FunctionBlocks = [startParameter.Dataflow.FunctionBlocks[1], startParameter.Dataflow.FunctionBlocks[0],],
            };
            Backend.Engines.CommunicationContracts.FunctionBlockConnector expectedInputConnector = new()
            {
                CrossSourcePoolingOptions = new()
                {
                    Strategy = Backend.Engines.CommunicationContracts.PoolingStrategy.Aggregate,
                    AggregatingPooling = JsonNode.Parse($$"""
                    {
                        "Type": "{{typeof(ViciOne.Engine.DefaultPoolings.OrPooling).AssemblyQualifiedName!}}",
                        "Value": "{\"$id\":\"1\"}"
                    }
                    """),
                },
                DesignIdentifier = inputConnector.DesignId.ToString(),
                UniqueIdentifier = inputConnector.Id.ToString(),
                InitialValue = JsonNode.Parse($$"""
                {
                    "Type": "{{typeof(int).AssemblyQualifiedName!}}",
                    "Value": "1000"
                }
                """),
                MarkAsChangedOnlyIfNotEqual = true,
                PerSourcePoolingOptions = new()
                {
                    Strategy = Backend.Engines.CommunicationContracts.PoolingStrategy.Aggregate,
                    AggregatingPooling = JsonNode.Parse($$"""
                    {
                        "Type": "{{typeof(ViciOne.Engine.DefaultPoolings.AndPooling).AssemblyQualifiedName!}}",
                        "Value": "{\"$id\":\"1\"}"
                    }
                    """),
                },
                Validators =
                [
                    JsonNode.Parse($$$"""
                    {
                        "Type": "{{{typeof(ViciOne.Engine.DefaultValidators.StringLengthValidator).AssemblyQualifiedName!}}}",
                        "Value": "{\"$id\":\"1\",\"MaxLength\":2147483647,\"MinLength\":0}"
                    }
                    """)!,
                ],
            };
            Backend.Engines.CommunicationContracts.FunctionBlockConnector expectedOutputConnector = new()
            {
                CrossSourcePoolingOptions = new()
                {
                    Strategy = Backend.Engines.CommunicationContracts.PoolingStrategy.Concat,
                },
                DesignIdentifier = outputConnector.DesignId.ToString(),
                UniqueIdentifier = outputConnector.Id.ToString(),
                InitialValue = JsonNode.Parse($$"""
                {
                    "Type": "{{typeof(double).AssemblyQualifiedName!}}",
                    "Value": "10.23"
                }
                """),
                MarkAsChangedOnlyIfNotEqual = true,
                PerSourcePoolingOptions = new()
                {
                    Strategy = Backend.Engines.CommunicationContracts.PoolingStrategy.Concat,
                },
            };
            Backend.Engines.CommunicationContracts.FunctionBlockConnector expectedInputConnectorSetting = new()
            {
                DesignIdentifier = inputConnectorSetting.DesignId.ToString(),
                UniqueIdentifier = inputConnectorSetting.Id.ToString(),
                InitialValue = JsonNode.Parse("""
                {
                    "Type": "my-type",
                    "Value": "{\"Value\":500}"
                }
                """),
            };
            Assert.Equal(new Backend.Engines.CommunicationContracts.Dataflow
            {
                FunctionBlocks =
                [
                    new()
                    {
                        DesignIdentifier = "10000000-1111-0000-0000-000000000000",
                        UniqueIdentifier = "20000000-0000-0000-0000-000000000000",
                    },
                    new()
                    {
                        DesignIdentifier = "10000000-1111-0000-0000-000000000000",
                        UniqueIdentifier = "10000000-0000-0000-0000-000000000000",
                        Name = "test fb",
                        RunMode = Backend.Engines.CommunicationContracts.FunctionBlockRunMode.Cycle,
                        CycleFrequency = 10,
                        Connectors =
                        [
                            expectedInputConnector,
                            expectedOutputConnector,
                            expectedInputConnectorSetting,
                        ],
                        Settings =
                        [
                            new()
                            {
                                DesignIdentifier = "10000000-1111-1111-0000-000000000001",
                                UniqueIdentifier = "10000000-0000-1111-0000-000000000001",
                                Timestamp = new DateTime(2021, 08, 23),
                                Value = JsonNode.Parse("""
                                {
                                    "Type": "my-type",
                                    "Value": "{\"Value\": 500}"
                                }
                                """),
                            },
                        ],
                        Variables =
                        [
                            new()
                            {
                                DesignIdentifier = "10000000-1111-0000-1111-000000000001",
                                UniqueIdentifier = "10000000-0000-0000-1111-000000000001",
                                Timestamp = new DateTime(2021, 08, 24),
                                Value = JsonNode.Parse($$"""
                                {
                                    "Type": "{{typeof(bool).AssemblyQualifiedName!}}",
                                    "Value": "true"
                                }
                                """),
                            },
                        ],
                    },
                ],
                AdditionalConnectorDesigns =
                [
                    new()
                    {
                        Connector = expectedInputConnector,
                        IsInput = true,
                        Name = "AdditionalInput",
                        ValueType = typeof(int).AssemblyQualifiedName!,
                    },
                ],
                Containers = [expectedContainer1, expectedContainer2,],
                ContainerHierarchy = [new() { Container = expectedContainer1, Containers = [expectedContainer2,], },],
                Links =
                {
                    new() // Empfang von eigener Engine
                    {
                        Converter = JsonNode.Parse($$"""
                        {
                            "Type": "{{typeof(DoubleConverter).AssemblyQualifiedName!}}",
                            "Value": "{\"$id\":\"1\"}"
                        }
                        """),
                        Destination = expectedInputConnector,
                        Source = expectedOutputConnector,
                        UniqueIdentifier = "10000000-2222-0000-0000-000000000001",
                    },
                }
            }, startParameter.Dataflow, new JsonEqualityComparer<Backend.Engines.CommunicationContracts.Dataflow>());
            startParameter.EngineLog.Should().Be(new Backend.Engines.CommunicationContracts.EngineLogConfiguration
            {
                Communication = new()
                {
                    Id = "engine-log-communication",
                    Properties =
                    [
                        new("engine-log-communication-property") { Value = TypedObject.CreateMandatory("engine-log-communication-property-value").ToJsonNode(), },
                    ],
                },
                LogLevel = LogLevel.Debug,
            }, new JsonEqualityComparer<Backend.Engines.CommunicationContracts.EngineLogConfiguration>());
            startParameter.DataDistribution.ExternalConnections.Should().SatisfyRespectively(
                externalRemote =>
                {
                    externalRemote.UniqueIdentifier.Should().NotBeNullOrEmpty().And.NotBe(Guid.Empty.ToString());
                    externalRemote.Name.Should().Be(remoteText);
                    externalRemote.Communication.Should().Be(new Backend.Engines.CommunicationContracts.CommunicationConfiguration()
                    {
                        Id = MqttCommunicationId,
                        Properties =
                        [
                            new(MqttCommunicationPropertyUri) { Value = TypedObject.CreateMandatory(new Uri("ws://remote/data")).ToJsonNode(), },
                            new(MqttCommunicationPropertyUsername) { Value = TypedObject.CreateMandatory("user").ToJsonNode(), },
                            new(MqttCmmunicationPropertyPassword) { Value = TypedObject.CreateMandatory("123-.").ToJsonNode(), },
                            new(MqttCommunicationPropertyMaxPendingMessages) { Value = TypedObject.CreateMandatory(5_000).ToJsonNode(), },
                            new(MqttCommunicationPropertyQualityOfService) { Value = TypedObject.CreateMandatory((short)1).ToJsonNode(), },
                            new(MqttCommunicationPropertyRetain) { Value = TypedObject.CreateMandatory(true).ToJsonNode(), },
                        ],
                    }, new JsonEqualityComparer<Backend.Engines.CommunicationContracts.CommunicationConfiguration>());
                    externalRemote.IncomingConnectors.Should().BeEquivalentTo(new Backend.Engines.CommunicationContracts.IncomingConnector[]
                    {
                        new() // Empfang von anderer Engine
                        {
                            Connector = expectedInputConnector,
                            Channel = outputConnectorOfOtherEngine.ToString(),
                            SourceType = typeof(double).AssemblyQualifiedName!,
                            Converter = new DoubleConverter(),
                        },
                    }, opt => opt.IgnoringCyclicReferences());
                    externalRemote.OutgoingConnectors.Should().BeEmpty();
                },
                externalMesh =>
                {
                    externalMesh.UniqueIdentifier.Should().NotBeNullOrEmpty().And.NotBe(Guid.Empty.ToString());
                    externalMesh.Name.Should().Be(meshText);
                    externalMesh.Communication.Should().Be(new Backend.Engines.CommunicationContracts.CommunicationConfiguration()
                    {
                        Id = MqttCommunicationId,
                        Properties =
                        [
                            new(MqttCommunicationPropertyHost) { Value = TypedObject.CreateMandatory("mesh").ToJsonNode(), },
                            new(MqttCommunicationPropertyPort) { Value = TypedObject.CreateMandatory(9000).ToJsonNode(), },
                            new(MqttCommunicationPropertyUsername) { Value = TypedObject.CreateMandatory("admin").ToJsonNode(), },
                            new(MqttCmmunicationPropertyPassword) { Value = TypedObject.CreateMandatory("789#").ToJsonNode(), },
                            new(MqttCommunicationPropertyMaxPendingMessages) { Value = TypedObject.CreateMandatory(10_000).ToJsonNode(), },
                            new(MqttCommunicationPropertyQualityOfService) { Value = TypedObject.CreateMandatory((short)2).ToJsonNode(), },
                            new(MqttCommunicationPropertyRetain) { Value = TypedObject.CreateMandatory(true).ToJsonNode(), },
                        ],
                    }, new JsonEqualityComparer<Backend.Engines.CommunicationContracts.CommunicationConfiguration>());
                    externalMesh.IncomingConnectors.Should().BeEquivalentTo(new Backend.Engines.CommunicationContracts.IncomingConnector[]
                    {
                        new() // Empfang von DataPort
                        {
                            Connector = expectedInputConnector,
                            Channel = dataPortNode.ToString(),
                            SourceType = typeof(int).AssemblyQualifiedName!,
                        },
                    }, opt => opt.IgnoringCyclicReferences());
                    externalMesh.OutgoingConnectors.Should().BeEquivalentTo(new Backend.Engines.CommunicationContracts.OutgoingConnector[]
                    {
                        new() // Protokollierung von Prozessdaten
                        {
                            Connector = expectedInputConnector,
                            Channel = inputConnector.Id.ToString(),
                        },
                    }, opt => opt.IgnoringCyclicReferences());
                },
                externalLocal =>
                {
                    externalLocal.UniqueIdentifier.Should().NotBeNullOrEmpty().And.NotBe(Guid.Empty.ToString());
                    externalLocal.Name.Should().Be(localText);
                    externalLocal.Communication.Should().Be(new Backend.Engines.CommunicationContracts.CommunicationConfiguration()
                    {
                        Id = MqttCommunicationId,
                        Properties =
                        [
                            new(MqttCommunicationPropertyHost) { Value = TypedObject.CreateMandatory("local").ToJsonNode(), },
                            new(MqttCommunicationPropertyPort) { Value = TypedObject.CreateMandatory(1883).ToJsonNode(), },
                        ],
                    }, new JsonEqualityComparer<Backend.Engines.CommunicationContracts.CommunicationConfiguration>());
                    externalLocal.IncomingConnectors.Should().BeEmpty();
                    externalLocal.OutgoingConnectors.Should().BeEquivalentTo(new Backend.Engines.CommunicationContracts.OutgoingConnector[]
                    {
                        new() // Senden an andere Engine
                        {
                            Connector = expectedOutputConnector,
                            Channel = outputConnector.Id.ToString(),
                        },
                        new() // Senden an DataPort
                        {
                            Connector = expectedOutputConnector,
                            Channel = dataPortNode.ToString(),
                        },
                    }, opt => opt.IgnoringCyclicReferences());
                });
            startParameter.DataDistribution.RuntimeFunctionCommunications.Should().SatisfyRespectively(
                mqtt => mqtt.Should().Be(new Backend.Engines.CommunicationContracts.CommunicationConfiguration()
                {
                    Id = MqttCommunicationId,
                    Properties =
                    [
                        new(MqttCommunicationPropertyHost) { Value = TypedObject.CreateMandatory("local").ToJsonNode(), },
                        new(MqttCommunicationPropertyPort) { Value = TypedObject.CreateMandatory(1883).ToJsonNode(), },
                        new(MqttCommunicationPropertyUsername) { Value = default, },
                        new(MqttCmmunicationPropertyPassword) { Value = default, },
                        new(MqttCommunicationPropertyMaxPendingMessages) { Value = TypedObject.CreateMandatory(10_000).ToJsonNode(), },
                        new(MqttCommunicationPropertyQualityOfService) { Value = TypedObject.CreateMandatory((short)2).ToJsonNode(), },
                        new(MqttCommunicationPropertyRetain) { Value = TypedObject.CreateMandatory(false).ToJsonNode(), },
                    ],
                }, new JsonEqualityComparer<Backend.Engines.CommunicationContracts.CommunicationConfiguration>()));
            startParameter.DataDistribution.SettingsPersistenceCommunications.Should().SatisfyRespectively(
                fileSystem => fileSystem.Should().Be(new Backend.Engines.CommunicationContracts.PersistenceConfiguration()
                {
                    Communication = new()
                    {
                        Id = FileSystemCommunicationId,
                        Properties =
                        [
                            new(FileSystemCommunicationPropertyDirectory) { Value = TypedObject.CreateMandatory(Path.Combine("persistence", "settings", engineId.ToString())).ToJsonNode(), },
                        ],
                    },
                    IntervalCycles = 1,
                }, new JsonEqualityComparer<Backend.Engines.CommunicationContracts.PersistenceConfiguration>()),
                mqtt => mqtt.Should().Be(new Backend.Engines.CommunicationContracts.PersistenceConfiguration()
                {
                    Communication = new()
                    {
                        Id = MqttCommunicationId,
                        Properties =
                        [
                            new(MqttCommunicationPropertyHost) { Value = TypedObject.CreateMandatory("local").ToJsonNode(), },
                            new(MqttCommunicationPropertyPort) { Value = TypedObject.CreateMandatory(1883).ToJsonNode(), },
                            new(MqttCommunicationPropertyUsername) { Value = default, },
                            new(MqttCmmunicationPropertyPassword) { Value = default, },
                            new(MqttCommunicationPropertyMaxPendingMessages) { Value = TypedObject.CreateMandatory(10_000).ToJsonNode(), },
                            new(MqttCommunicationPropertyQualityOfService) { Value = TypedObject.CreateMandatory((short)1).ToJsonNode(), },
                            new(MqttCommunicationPropertyRetain) { Value = TypedObject.CreateMandatory(true).ToJsonNode(), },
                        ],
                    },
                    IntervalCycles = 1,
                }, new JsonEqualityComparer<Backend.Engines.CommunicationContracts.PersistenceConfiguration>()));
            startParameter.DataDistribution.VariablesPersistenceCommunications.Should().SatisfyRespectively(
                fileSystem => fileSystem.Should().Be(new Backend.Engines.CommunicationContracts.PersistenceConfiguration()
                {
                    Communication = new()
                    {
                        Id = FileSystemCommunicationId,
                        Properties =
                        [
                            new(FileSystemCommunicationPropertyDirectory) { Value = TypedObject.CreateMandatory(Path.Combine("persistence", "variables", engineId.ToString())).ToJsonNode(), },
                        ],
                    },
                    IntervalCycles = 3,
                }, new JsonEqualityComparer<Backend.Engines.CommunicationContracts.PersistenceConfiguration>()),
                mqtt => mqtt.Should().Be(new Backend.Engines.CommunicationContracts.PersistenceConfiguration()
                {
                    Communication = new()
                    {
                        Id = MqttCommunicationId,
                        Properties =
                        [
                            new(MqttCommunicationPropertyHost) { Value = TypedObject.CreateMandatory("local").ToJsonNode(), },
                            new(MqttCommunicationPropertyPort) { Value = TypedObject.CreateMandatory(1883).ToJsonNode(), },
                            new(MqttCommunicationPropertyUsername) { Value = default, },
                            new(MqttCmmunicationPropertyPassword) { Value = default, },
                            new(MqttCommunicationPropertyMaxPendingMessages) { Value = TypedObject.CreateMandatory(10_000).ToJsonNode(), },
                            new(MqttCommunicationPropertyQualityOfService) { Value = TypedObject.CreateMandatory((short)1).ToJsonNode(), },
                            new(MqttCommunicationPropertyRetain) { Value = TypedObject.CreateMandatory(true).ToJsonNode(), },

                        ],
                    },
                    IntervalCycles = 12,
                }, new JsonEqualityComparer<Backend.Engines.CommunicationContracts.PersistenceConfiguration>()));
        }
    }
}

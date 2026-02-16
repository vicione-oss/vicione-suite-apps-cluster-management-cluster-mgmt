using ClusterManagement.Backend.Engines;
using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Tests.Engines;

public class WorkflowComparerFacts
{
    public class Equals_ : WorkflowComparerFacts
    {
        [Fact]
        public void Returns_true_if_both_are_null() => new WorkflowComparer().Equals(null, null).Should().BeTrue();

        [Fact]
        public void Returns_false_if_one_is_null()
        {
            new WorkflowComparer().Equals(new(), null).Should().BeFalse();
            new WorkflowComparer().Equals(null, new()).Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(Equal_data))]
        public void Returns_true_if_equal(Workflow x, Workflow y) => new WorkflowComparer().Equals(x, y).Should().BeTrue();

        [Theory]
        [MemberData(nameof(Unequal_data))]
        public void Returns_false_if_unequal(Workflow x, Workflow y) => new WorkflowComparer().Equals(x, y).Should().BeFalse();
    }

    public class GetHashCode_ : WorkflowComparerFacts
    {
        [Theory]
        [MemberData(nameof(Equal_data))]
        public void Returns_same_hash_if_equal(Workflow x, Workflow y)
        {
            var workflowComparer = new WorkflowComparer();
            workflowComparer.GetHashCode(y).Should().Be(workflowComparer.GetHashCode(x));
        }

        [Theory]
        [MemberData(nameof(Unequal_data))]
        public void Returns_false_if_unequal(Workflow x, Workflow y)
        {
            var workflowComparer = new WorkflowComparer();
            workflowComparer.GetHashCode(y).Should().NotBe(workflowComparer.GetHashCode(x));
        }
    }

    public static TheoryData<Workflow, Workflow> Equal_data()
    {
        var first = FullWorkflow();
        return new TheoryData<Workflow, Workflow>()
        {
            { first, FullWorkflow() },
            { first, ChangeWorkflow(_ => _.ClusterDependencies.Reverse()) },
            { first, ChangeWorkflow(_ => _.Containers.Reverse()) },
            { first, ChangeWorkflow(_ => _.FunctionBlockConnectorLinks.Reverse()) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors.Reverse()) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Settings.Reverse()) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Variables.Reverse()) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks.Reverse()) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].IncomingLinks.Reverse()) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].OutgoingLinks.Reverse()) },
        };
    }

    public static TheoryData<Workflow, Workflow> Unequal_data()
    {
        var first = FullWorkflow();
        return new TheoryData<Workflow, Workflow>()
        {
            { first, ChangeWorkflow(_ => _.ClusterDependencies[0] = new(string.Empty, _.ClusterDependencies[0].Version)) },
            { first, ChangeWorkflow(_ => _.ClusterDependencies[0] = new(_.ClusterDependencies[0].Name, string.Empty)) },
            { first, ChangeWorkflow(_ => _.ClusterDependencies.Clear()) },
            { first, ChangeWorkflow(_ => _.Containers[0].Name = string.Empty) },
            { first, ChangeWorkflow(_ => _.Containers[0].ParentContainerId = Guid.Empty) },
            { first, ChangeWorkflow(_ => _.Containers[0].ParentContainerId = default) },
            { first, ChangeWorkflow(_ => _.Containers[0].Id = default) },
            { first, ChangeWorkflow(_ => _.Containers.Clear()) },
            { first, ChangeWorkflow(_ => _.Engine.ChainIndex = default) },
            { first, ChangeWorkflow(_ => _.Engine.CycleTime = default) },
            { first, ChangeWorkflow(_ => _.Engine.Id = Guid.NewGuid()) },
            { first, ChangeWorkflow(_ => _.Engine.LogLevel = default) },
            { first, ChangeWorkflow(_ => _.Engine.Name = string.Empty) },
            { first, ChangeWorkflow(_ => _.EngineLogCommunication.Id = string.Empty) },
            { first, ChangeWorkflow(_ => _.EngineLogCommunication.Properties[0] = new(string.Empty) { Value = _.ExternalConnections[0].Communication.Properties[0].Value, }) },
            { first, ChangeWorkflow(_ => _.EngineLogCommunication.Properties[0].Value!.Type = string.Empty) },
            { first, ChangeWorkflow(_ => _.EngineLogCommunication.Properties[0].Value!.Value = string.Empty) },
            { first, ChangeWorkflow(_ => _.EngineLogCommunication.Properties[0].Value!.Value = default) },
            { first, ChangeWorkflow(_ => _.EngineLogCommunication.Properties[0].Value = default) },
            { first, ChangeWorkflow(_ => _.EngineLogCommunication.Properties.Clear()) },
            { first, ChangeWorkflow(_ => _.FunctionBlockConnectorLinks[0].DestinationConnectorId = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlockConnectorLinks[0].SourceConnectorId = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlockConnectorLinks[0].TypeConverter!.Type = string.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlockConnectorLinks[0].TypeConverter!.Value = string.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlockConnectorLinks[0].TypeConverter!.Value = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlockConnectorLinks[0].TypeConverter = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlockConnectorLinks[0].Id = Guid.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlockConnectorLinks.Clear()) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].CrossSourceAggregatingPooling!.Type = string.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].CrossSourceAggregatingPooling!.Value = string.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].CrossSourceAggregatingPooling!.Value = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].CrossSourceAggregatingPooling = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].CrossSourcePoolingStrategy = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].DesignId = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].Id = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].InitialValue!.Type = string.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].InitialValue!.Value = string.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].InitialValue!.Value = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].InitialValue = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].MarkAsChangedOnlyIfNotEqual = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].PerSourceAggregatingPooling!.Type = string.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].PerSourceAggregatingPooling!.Value = string.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].PerSourceAggregatingPooling!.Value = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].PerSourceAggregatingPooling = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].PerSourcePoolingStrategy = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].Validator!.Type = string.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].Validator!.Value = string.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].Validator!.Value = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors[0].Validator = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Connectors.Clear()) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].ContainerId = Guid.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].ContainerId = null) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].CycleFrequency = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].DesignId = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Name = string.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].RunIndex = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].RunMode = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Settings[0].DesignId = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Settings[0].Timestamp = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Settings[0].Id = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Settings[0].Value!.Type = string.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Settings[0].Value!.Value = string.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Settings[0].Value!.Value = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Settings[0].Value = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Settings.Clear()) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Id = Guid.NewGuid()) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Variables[0].DesignId = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Variables[0].Timestamp = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Variables[0].Id = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Variables[0].Value!.Type = string.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Variables[0].Value!.Value = string.Empty) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Variables[0].Value!.Value = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Variables[0].Value = default) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks[0].Variables.Clear()) },
            { first, ChangeWorkflow(_ => _.FunctionBlocks.Clear()) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].Name = string.Empty) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].Name = default) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].Communication.Id = string.Empty) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].Communication.Properties[0] = new(string.Empty) { Value = _.ExternalConnections[0].Communication.Properties[0].Value, }) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].Communication.Properties[0].Value!.Type = string.Empty) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].Communication.Properties[0].Value!.Value = string.Empty) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].Communication.Properties[0].Value!.Value = default) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].Communication.Properties[0].Value = default) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].Communication.Properties.Clear()) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].IncomingLinks[0].ConnectorId = default) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].IncomingLinks[0].RemoteId = default) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].IncomingLinks[0].SourceType = string.Empty) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].IncomingLinks[0].TypeConverter!.Type = string.Empty) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].IncomingLinks[0].TypeConverter!.Value = string.Empty) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].IncomingLinks[0].TypeConverter!.Value = default) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].IncomingLinks[0].TypeConverter = default) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].IncomingLinks.Clear()) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].OutgoingLinks[0].ConnectorId = default) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].OutgoingLinks[0].RemoteId = default) },
            { first, ChangeWorkflow(_ => _.ExternalConnections[0].OutgoingLinks.Clear()) },
            { first, ChangeWorkflow(_ => _.ExternalConnections.Clear()) },
            { first, ChangeWorkflow(_ => _.PersistenceCommunication.Address = string.Empty) },
            { first, ChangeWorkflow(_ => _.PersistenceCommunication.Password = string.Empty) },
            { first, ChangeWorkflow(_ => _.PersistenceCommunication.Password = default) },
            { first, ChangeWorkflow(_ => _.PersistenceCommunication.Protocol = default) },
            { first, ChangeWorkflow(_ => _.PersistenceCommunication.TcpPort = 0) },
            { first, ChangeWorkflow(_ => _.PersistenceCommunication.TcpPort = default) },
            { first, ChangeWorkflow(_ => _.PersistenceCommunication.Username = string.Empty) },
            { first, ChangeWorkflow(_ => _.PersistenceCommunication.Username = default) },
            { first, ChangeWorkflow(_ => _.PersistenceCommunication.QualityOfService = 0) },
            { first, ChangeWorkflow(_ => _.PersistenceCommunication.Retain = false) },
            { first, ChangeWorkflow(_ => _.RuntimeFunctionsCommunication.Address = string.Empty) },
            { first, ChangeWorkflow(_ => _.RuntimeFunctionsCommunication.Password = string.Empty) },
            { first, ChangeWorkflow(_ => _.RuntimeFunctionsCommunication.Password = default) },
            { first, ChangeWorkflow(_ => _.RuntimeFunctionsCommunication.Protocol = default) },
            { first, ChangeWorkflow(_ => _.RuntimeFunctionsCommunication.TcpPort = 0) },
            { first, ChangeWorkflow(_ => _.RuntimeFunctionsCommunication.TcpPort = default) },
            { first, ChangeWorkflow(_ => _.RuntimeFunctionsCommunication.Username = string.Empty) },
            { first, ChangeWorkflow(_ => _.RuntimeFunctionsCommunication.Username = default) },
            { first, ChangeWorkflow(_ => _.RuntimeFunctionsCommunication.QualityOfService = 0) },
            { first, ChangeWorkflow(_ => _.RuntimeFunctionsCommunication.Retain = false) },
        };
    }

    private static Workflow FullWorkflow()
    {
        var guid = 0;
        Guid GenerateGuid() => Guid.Parse($"00000000-0000-0000-0000-{++guid}".PadRight(36, '0'));

        return new()
        {
            Containers =
            [
                new()
                {
                    Name = nameof(Workflow.Containers),
                    ParentContainerId = GenerateGuid(),
                    Id = GenerateGuid(),
                },
                new(),
            ],
            Engine = new()
            {
                ChainIndex = 1,
                CycleTime = 3,
                Id = GenerateGuid(),
                LogLevel = LogLevel.Warning,
                Name = nameof(Workflow.Engine),
            },
            EngineLogCommunication = new()
            {
                Id = "UnknownId",
                Properties =
                [
                    new("UnknownProperty") { Value = new("UnknownType", "{}"), },
                ],
            },
            FunctionBlockConnectorLinks =
            [
                new()
                {
                    DestinationConnectorId = GenerateGuid(),
                    SourceConnectorId = GenerateGuid(),
                    TypeConverter = new("UnknownType", "{}"),
                    Id = GenerateGuid(),
                },
                new(),
            ],
            FunctionBlocks =
            [
                new()
                {
                    Connectors =
                    [
                        new()
                        {
                            CrossSourceAggregatingPooling = new("UnknownType", "{}"),
                            CrossSourcePoolingStrategy = ViciOne.Core.Contracts.DataModel.PoolingStrategy.Aggregate,
                            DesignId = GenerateGuid(),
                            Id = GenerateGuid(),
                            InitialValue = new("UnknownType", "{}"),
                            MarkAsChangedOnlyIfNotEqual = true,
                            PerSourceAggregatingPooling = new("UnknownType", "{}"),
                            PerSourcePoolingStrategy = ViciOne.Core.Contracts.DataModel.PoolingStrategy.Aggregate,
                            Validator = new("UnknownType", "{}"),
                        },
                        new(),
                    ],
                    ContainerId = GenerateGuid(),
                    CycleFrequency = 5,
                    DesignId = GenerateGuid(),
                    Name = nameof(Workflow.FunctionBlocks),
                    RunIndex = 2,
                    RunMode = ViciOne.Cluster.Model.FunctionBlockRunMode.Cyclic,
                    Settings =
                    [
                        new()
                        {
                            DesignId = GenerateGuid(),
                            Timestamp = DateTime.MaxValue,
                            Id = GenerateGuid(),
                            Value = new("UnknownType", "{}"),
                        },
                        new(),
                    ],
                    Id = GenerateGuid(),
                    Variables =
                    [
                        new()
                        {
                            DesignId = GenerateGuid(),
                            Timestamp = DateTime.MaxValue,
                            Id = GenerateGuid(),
                            Value = new("UnknownType", "{}"),
                        },
                        new(),
                    ],
                },
                new(),
            ],
            ExternalConnections =
            [
                new()
                {
                    Name = "ConnectionName",
                    Communication = new()
                    {
                        Id = "UnknownId",
                        Properties =
                        [
                            new("UnknownProperty") { Value = new("UnknownType", "{}"), },
                        ],
                    },
                    IncomingLinks =
                    [
                        new()
                        {
                            ConnectorId = GenerateGuid(),
                            RemoteId = GenerateGuid(),
                            SourceType = "UnknownType",
                            TypeConverter = new("UnknownType", "{}"),
                        },
                        new(),
                    ],
                    OutgoingLinks =
                    [
                        new()
                        {
                            ConnectorId = GenerateGuid(),
                            RemoteId = GenerateGuid(),
                        },
                        new(),
                    ],
                },
            ],
            ClusterDependencies =
            [
                new("A", "1.2.3"),
                new(string.Empty, string.Empty),
            ],
            PersistenceCommunication = new()
            {
                Address = "ws://",
                Protocol = MqttProtocol.WebSocket,
                Password = "abc",
                TcpPort = 1883,
                Username = "bcd",
                QualityOfService = 1,
                Retain = true,
            },
            RuntimeFunctionsCommunication = new()
            {
                Address = "ws://",
                Protocol = MqttProtocol.WebSocket,
                Password = "abc",
                TcpPort = 1883,
                Username = "bcd",
                QualityOfService = 1,
                Retain = true,
            },
        };
    }

    private static Workflow ChangeWorkflow(Action<Workflow> change)
    {
        var workflow = FullWorkflow();
        change(workflow);
        return workflow;
    }
}

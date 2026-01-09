using ClusterManagement.Backend.EngineHosts.CommunicationContracts;
using ViciOne.Core.Contracts.DataModel;

namespace ClusterManagement.Backend.Engines;

internal static class WorkflowTranslator
{
    internal static CommunicationContracts.StartParameter ToStartParameter(this Workflow workflow) => new()
    {
        Dataflow = workflow.ToDataflow(out var connectors),
        EngineLog = workflow.ToEngineLog(),
        DataDistribution = workflow.ToDataDistribution(connectors),
        Engine = workflow.Engine.ToEngine(),
    };

    internal static CommunicationContracts.Dataflow ToDataflow(this Workflow workflow, out IDictionary<Guid, CommunicationContracts.FunctionBlockConnector> connectors)
    {
        CommunicationContracts.Dataflow dataflow = new();

        Dictionary<FunctionBlock, CommunicationContracts.FunctionBlock> functionBlocks = [];
        connectors = new Dictionary<Guid, CommunicationContracts.FunctionBlockConnector>();

        foreach (var fb in workflow.FunctionBlocks.OrderBy(fb => fb.RunIndex))
        {
            CommunicationContracts.FunctionBlock functionBlock = new()
            {
                UniqueIdentifier = fb.Id.ToString(),
                DesignIdentifier = fb.DesignId.ToString(),
                Name = fb.Name,
                CycleFrequency = fb.CycleFrequency,
                RunMode = fb.RunMode.ToCommunicationContracts(),
                Settings = [.. fb.Settings.Select(s => s.ToCommunicationContracts())],
                Variables = [.. fb.Variables.Select(v => v.ToCommunicationContracts())],
            };
            functionBlocks.Add(fb, functionBlock);

            foreach (var c in fb.Connectors)
            {
                var connector = c.ToCommunicationContracts();
                functionBlock.Connectors.Add(connector);
                connectors.Add(c.Id, connector);

                if (c.CrossSourcePoolingStrategy.HasValue)
                {
                    connector.CrossSourcePoolingOptions = new()
                    {
                        Strategy = c.CrossSourcePoolingStrategy.Value.ToCommunicationContracts(),
                        AggregatingPooling = c.CrossSourceAggregatingPooling?.ToJsonNode(),
                    };
                }
                if (c.PerSourcePoolingStrategy.HasValue)
                {
                    connector.PerSourcePoolingOptions = new()
                    {
                        Strategy = c.PerSourcePoolingStrategy.Value.ToCommunicationContracts(),
                        AggregatingPooling = c.PerSourceAggregatingPooling?.ToJsonNode(),
                    };
                }

                if (c.Validator is not null)
                    connector.Validators.Add(c.Validator.ToJsonNode());
            }
        }
        dataflow.FunctionBlocks.AddRange(functionBlocks.Values);

        var containers = workflow.Containers.ToDictionary(c => c.Id, c => new CommunicationContracts.Container
        {
            UniqueIdentifier = c.Id.ToString(),
            Name = c.Name,
            FunctionBlocks = [.. workflow.FunctionBlocks
                .Where(fb => fb.ContainerId == c.Id)
                .Select(fb => functionBlocks[fb])],
        });
        dataflow.Containers.AddRange(containers.Values);
        dataflow.ContainerHierarchy.AddRange([.. workflow.Containers
            .Where(c => c.ParentContainerId is not null)
            .GroupBy(c => (Guid)c.ParentContainerId!)
            .Select(g => new CommunicationContracts.ContainerAssignment
            {
                Container = containers[g.Key],
                Containers = [.. g.Select(c => containers[c.Id])],
            })]);

        foreach (var link in workflow.FunctionBlockConnectorLinks)
        {
            CommunicationContracts.ConnectorLink convertedLink = new()
            {
                Source = connectors[link.SourceConnectorId],
                Destination = connectors[link.DestinationConnectorId],
                UniqueIdentifier = link.Id.ToString(),
                Converter = link.TypeConverter?.ToJsonNode(),
            };
            dataflow.Links.Add(convertedLink);
        }

        foreach (var additionalConnectorDesign in workflow.AdditionalConnectorDesigns)
        {
            dataflow.AdditionalConnectorDesigns.Add(new()
            {
                Connector = connectors[additionalConnectorDesign.ConnectorId],
                IsInput = additionalConnectorDesign.IsInput,
                Name = additionalConnectorDesign.Name,
                ValueType = additionalConnectorDesign.ValueType.AssemblyQualifiedName ?? string.Empty,
            });
        }

        return dataflow;
    }
    private static CommunicationContracts.FunctionBlockRunMode ToCommunicationContracts(this ViciOne.Cluster.Model.FunctionBlockRunMode runMode)
        => runMode switch
        {
            ViciOne.Cluster.Model.FunctionBlockRunMode.Change => CommunicationContracts.FunctionBlockRunMode.Change,
            ViciOne.Cluster.Model.FunctionBlockRunMode.Cyclic => CommunicationContracts.FunctionBlockRunMode.Cycle,
            _ => throw new NotSupportedException($"Cannot convert run mode '{runMode}'."),
        };

    private static CommunicationContracts.FunctionBlockConnector ToCommunicationContracts(this FunctionBlockConnector connector)
        => new()
        {
            UniqueIdentifier = connector.Id.ToString(),
            DesignIdentifier = connector.DesignId.ToString(),
            InitialValue = connector.InitialValue?.ToJsonNode(),
            MarkAsChangedOnlyIfNotEqual = connector.MarkAsChangedOnlyIfNotEqual,
        };

    private static CommunicationContracts.FunctionBlockSetting ToCommunicationContracts(this FunctionBlockSetting setting)
        => new()
        {
            UniqueIdentifier = setting.Id.ToString(),
            DesignIdentifier = setting.DesignId.ToString(),
            Timestamp = setting.Timestamp,
            Value = setting.Value?.ToJsonNode(),
        };

    private static CommunicationContracts.FunctionBlockVariable ToCommunicationContracts(this FunctionBlockVariable variable)
        => new()
        {
            UniqueIdentifier = variable.Id.ToString(),
            DesignIdentifier = variable.DesignId.ToString(),
            Timestamp = variable.Timestamp,
            Value = variable.Value?.ToJsonNode(),
        };

    private static CommunicationContracts.PoolingStrategy ToCommunicationContracts(this PoolingStrategy strategy)
        => strategy switch
        {
            PoolingStrategy.Queue => CommunicationContracts.PoolingStrategy.Queue,
            PoolingStrategy.Aggregate => CommunicationContracts.PoolingStrategy.Aggregate,
            PoolingStrategy.Concat => CommunicationContracts.PoolingStrategy.Concat,
            _ => throw new NotSupportedException($"Cannot convert pooling strategy '{strategy}'."),
        };

    private static CommunicationContracts.Engine ToEngine(this Engine engine)
        => new()
        {
            UniqueIdentifier = engine.Id.ToString(),
            Name = engine.Name,
        };

    private static CommunicationContracts.DataDistribution ToDataDistribution(this Workflow workflow, IDictionary<Guid, CommunicationContracts.FunctionBlockConnector> connectors)
    {
        CommunicationContracts.DataDistribution dataDistribution = new();

        foreach (var suiteExternalConnection in workflow.ExternalConnections)
        {
            var id = Guid.NewGuid().ToString();
            CommunicationContracts.ExternalConnection externalConnection = new()
            {
                UniqueIdentifier = id,
                Communication = suiteExternalConnection.Communication.ToCommunicationContracts(),
                Name = suiteExternalConnection.Name,
            };
            dataDistribution.ExternalConnections.Add(externalConnection);

            foreach (var incomingLink in suiteExternalConnection.IncomingLinks)
            {
                externalConnection.IncomingConnectors.Add(new()
                {
                    Connector = connectors[incomingLink.ConnectorId],
                    Channel = incomingLink.RemoteId.ToString(),
                    SourceType = incomingLink.SourceType,
                    Converter = incomingLink.TypeConverter?.ToJsonNode(),
                });
            }

            foreach (var outgoingLink in suiteExternalConnection.OutgoingLinks)
            {
                externalConnection.OutgoingConnectors.Add(new()
                {
                    Connector = connectors[outgoingLink.ConnectorId],
                    Channel = outgoingLink.RemoteId.ToString(),
                });
            }
        }

        dataDistribution.RuntimeFunctionCommunications.Add(workflow.RuntimeFunctionsCommunication.ToCommunicationConfiguration().ToCommunicationContracts());

        var persistenceCommunication = workflow.PersistenceCommunication.ToCommunicationConfiguration().ToCommunicationContracts();
        dataDistribution.SettingsPersistenceCommunications.Add(new()
        {
            Communication = new CommunicationConfiguration()
            {
                Id = "8B58C1F9-D120-42E9-8F63-B7C62FE1C415",
                Properties =
                [
                    new("Directory") { Value = TypedObject.CreateMandatory(Path.Combine("persistence", "settings", workflow.Engine.Id.ToString())), },
                ],
            }.ToCommunicationContracts(),
            IntervalCycles = 1,
        });
        dataDistribution.SettingsPersistenceCommunications.Add(new()
        {
            Communication = persistenceCommunication,
            IntervalCycles = 1,
        });
        dataDistribution.VariablesPersistenceCommunications.Add(new()
        {
            Communication = new CommunicationConfiguration()
            {
                Id = "8B58C1F9-D120-42E9-8F63-B7C62FE1C415",
                Properties =
                [
                    new("Directory") { Value = TypedObject.CreateMandatory(Path.Combine("persistence", "variables", workflow.Engine.Id.ToString())), },
                ],
            }.ToCommunicationContracts(),
            IntervalCycles = DetermineIntervalCycles(workflow, 24.0), // use "highly composite number" to match potentially many engine cycle times
        });
        dataDistribution.VariablesPersistenceCommunications.Add(new()
        {
            Communication = persistenceCommunication,
            IntervalCycles = DetermineIntervalCycles(workflow, 120.0), // use "highly composite number" to match potentially many engine cycle times
        });

        return dataDistribution;
    }

    private static int DetermineIntervalCycles(Workflow workflow, double criticalCycleTime)
        => Math.Max(1, (int)Math.Ceiling(criticalCycleTime / workflow.Engine.CycleTime));

    private static CommunicationContracts.EngineLogConfiguration ToEngineLog(this Workflow workflow)
        => new()
        {
            Communication = workflow.EngineLogCommunication.ToCommunicationContracts(),
            LogLevel = workflow.Engine.LogLevel,
        };

    internal static List<PackageReference> ToPackageReferences(this Workflow workflow)
        => [.. workflow.ClusterDependencies.Select(p => new PackageReference
        {
            Name = p.Name,
            Version = p.Version,
        })];

    private static CommunicationContracts.CommunicationConfiguration ToCommunicationContracts(this CommunicationConfiguration configuration)
    {
        CommunicationContracts.CommunicationConfiguration newConfiguration = new() { Id = configuration.Id, };
        foreach (var property in configuration.Properties)
            newConfiguration.Properties.Add(new(property.Id) { Value = property.Value?.ToJsonNode(), });
        return newConfiguration;
    }

    internal static CommunicationConfiguration ToCommunicationConfiguration(this MqttCommunication mqttCommunication)
    {
        CommunicationConfiguration configuration = new() { Id = "07AB9DE8-6216-41FE-9E94-D324C5C55F7A", };
        if (mqttCommunication.Protocol == MqttProtocol.TCP)
        {
            configuration.Properties.Add(new("Host") { Value = TypedObject.CreateMandatory(mqttCommunication.Address), });
            if (!mqttCommunication.TcpPort.HasValue)
                throw new InvalidOperationException($"MQTT communication with '{mqttCommunication.Address}' over TCP requires a port.");
            configuration.Properties.Add(new("Port") { Value = TypedObject.CreateMandatory(mqttCommunication.TcpPort.Value), });
        }
        else
        {
            configuration.Properties.Add(new("Uri") { Value = TypedObject.CreateMandatory(new Uri(mqttCommunication.Address)), });
        }
        configuration.Properties.Add(new("Username") { Value = TypedObject.CreateOptional(mqttCommunication.Username), });
        configuration.Properties.Add(new("Password") { Value = TypedObject.CreateOptional(mqttCommunication.Password), });
        configuration.Properties.Add(new("MaxPendingMessages") { Value = TypedObject.CreateMandatory(10_000), });
        configuration.Properties.Add(new("QualityOfService") { Value = TypedObject.CreateMandatory(mqttCommunication.QualityOfService), });
        configuration.Properties.Add(new("Retain") { Value = TypedObject.CreateMandatory(true), });
        return configuration;
    }
}

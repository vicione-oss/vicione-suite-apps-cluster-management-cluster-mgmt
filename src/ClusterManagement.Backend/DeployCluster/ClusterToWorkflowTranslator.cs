using System.Runtime.InteropServices;
using ClusterManagement.Backend.Engines;
using Sdk.Connections.Contracts;
using Sdk.Extensions;
using Sdk.Modules;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;
using ViciOne.Cluster.Model.Extensions;
using ViciOne.Core.Contracts;
using ViciOne.Core.Contracts.DataModel.Extensions;
using ViciOne.Suite.DataPort;
using ClusterDependency = ViciOne.Cluster.Model.ClusterDependency;
using Connector = ViciOne.Cluster.Model.Connector;
using Container = ViciOne.Cluster.Model.Container;
using Engine = ViciOne.Cluster.Model.Engine;
using FunctionBlock = ViciOne.Cluster.Model.FunctionBlock;
using Link = ViciOne.Cluster.Model.Link;

namespace ClusterManagement.Backend.DeployCluster;

internal static class ClusterToWorkflowTranslator
{
    internal static Workflow CreateWorkflow(Cluster cluster,
        EngineHost host,
        Engine engine,
        IDependencyResolver dependencyResolver,
        MqttConnection brokerConnection,
        IDictionary<Engine, List<DataPortTransformation>> enginesDataPorts,
        string engineLogDirectory)
    {
        var workflow = SetupWorkflow(engine, cluster.Dependencies);

        var brokerCommunicationAtMostOnce = TranslateMqttCommunication(brokerConnection, 0);
        var brokerCommunicationAtLeastOnce = TranslateMqttCommunication(brokerConnection, 1);
        var brokerCommunicationExactlyOnce = TranslateMqttCommunication(brokerConnection, 2);

        workflow.PersistenceCommunication = brokerCommunicationAtLeastOnce;
        workflow.RuntimeFunctionsCommunication = brokerCommunicationExactlyOnce;
        workflow.EngineLogCommunication = ConfigureEngineLog(host, engine, engineLogDirectory);

        ExternalConnection directConnection = new() { Name = "Direct between Engines", };
        ExternalConnection internalConnection = new() { Name = "Suite MQTT Broker", };

        TranslateFunctionBlocksAndContainers(workflow, cluster, host, engine.Id, dependencyResolver, enginesDataPorts,
            directConnection, internalConnection);

        enginesDataPorts.TryGetValue(engine, out var engineDataPorts);
        AddExternalCommunications(directConnection, internalConnection, engineDataPorts, workflow, brokerCommunicationAtMostOnce);
        AddAdditionalConnectorDesigns(engineDataPorts, workflow);

        return workflow;
    }

    private static CommunicationConfiguration ConfigureEngineLog(EngineHost host, Engine engine, string engineLogDirectory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new CommunicationConfiguration()
            {
                Id = "96B50074-1DF9-402C-90CF-AC9894C8E243",
                Properties =
                [
                    new("MetaDataFields")
                    {
                        Value = TypedObject.CreateMandatory(new Dictionary<string, object?>()
                        {
                            { "ENGINE_ID", engine.Id },
                            { "ENGINE_NAME", engine.Name },
                            { "ENGINE_HOST_NAME", host.Name },
                            { "ENGINE_HOST_ID", host.Id },
                            { "MODULE_ID", ModuleIdResolver.ResolveId<ClusterManagementBackendModule>() },
                        }),
                    },
                    new("OutputTemplate") { Value = TypedObject.CreateMandatory($$"""{{engine.Name}} ({{engine.Id}}): [{SourceContext}] {Message:lj}{NewLine}{Exception}"""), },
                ],
            };
        }
        else
        {
            return new CommunicationConfiguration()
            {
                Id = "E3601083-66AE-41FC-AC20-E05B1DD7936E",
                Properties =
                [
                    new("Path") { Value = TypedObject.CreateMandatory(Path.Combine(engineLogDirectory, $"{engine.Id}.log")), },
                ],
            };
        }
    }

    private static Workflow SetupWorkflow(Engine engine, IEnumerable<ClusterDependency> clusterDependencies)
    {
        Workflow workflow = new()
        {
            Engine =
            {
                ChainIndex = (int)engine.RunIndex,
                CycleTime = engine.MinCycleTime,
                Id = engine.Id,
                LogLevel = engine.LogLevel,
                Name = engine.Name,
            },
            ClusterDependencies = [.. clusterDependencies.Select(d => new Engines.ClusterDependency(d.Name, d.Version.ToString()))],
        };
        return workflow;
    }

    internal static MqttCommunication TranslateMqttCommunication(MqttConnection connection, short qualityOfServiceLevel = 0)
        => new()
        {
            Protocol = connection.Protocol == MqttConnectionType.WebSocket ? MqttProtocol.WebSocket : MqttProtocol.TCP,
            Address = connection.Address,
            TcpPort = connection.Port,
            Username = connection.Username,
            Password = connection.Password,
            QualityOfService = qualityOfServiceLevel,
        };

    private static void TranslateFunctionBlocksAndContainers(Workflow workflow, Cluster cluster, EngineHost host, Guid engineId,
        IDependencyResolver dependencyResolver, IDictionary<Engine, List<DataPortTransformation>> enginesDataPorts,
        ExternalConnection directConnection, ExternalConnection brokerConnection)
    {
        HashSet<Guid> translatedEngineContainers = [];
        var dataPortOutputDataTypes = enginesDataPorts
            .SelectMany(p => p.Value)
            .SelectMany(x => x.AdditionalConnectorDesigns)
            .Where(x => !x.IsInput)
            .ToDictionary(d => d.ConnectorId, d => d.ValueType);
        foreach (var dataflow in cluster.Dataflows)
        {
            foreach (var (container, containerPath) in dataflow.Root.TraverseDepthFirstWithPath(x => x.Containers))
            {
                var functionBlocksOfEngine = container.FunctionBlocks.Where(x => x.EngineId == engineId);
                var hasFunctionBlocks = false;
                foreach (var functionBlockOfEngine in functionBlocksOfEngine)
                {
                    hasFunctionBlocks = true;
                    TranslateFunctionBlock(host, workflow, functionBlockOfEngine, dependencyResolver, dataPortOutputDataTypes,
                        directConnection, brokerConnection);
                }
                if (hasFunctionBlocks)
                {
                    Container? parent = default;
                    foreach (var containerOfEngine in containerPath.Append(container))
                    {
                        if (translatedEngineContainers.Add(containerOfEngine.Id))
                        {
                            workflow.Containers.Add(new()
                            {
                                Name = containerOfEngine.Name,
                                ParentContainerId = parent?.Id,
                                Id = containerOfEngine.Id,
                            });
                        }
                        parent = containerOfEngine;
                    }
                }
            }
        }
        var runIndex = 0u;
        foreach (var functionBlock in workflow.FunctionBlocks.OrderBy(x => x.RunIndex))
            functionBlock.RunIndex = runIndex++;
    }

    private static void TranslateFunctionBlock(EngineHost host, Workflow workflow, FunctionBlock engineFunctionBlock,
        IDependencyResolver dependencyResolver, Dictionary<Guid, Type> dataPortOutputDataTypes,
        ExternalConnection directConnection, ExternalConnection brokerConnection)
    {
        Engines.FunctionBlock functionBlock = new()
        {
            ContainerId = engineFunctionBlock.Container.Id,
            CycleFrequency = engineFunctionBlock.CycleFrequency,
            DesignId = engineFunctionBlock.DesignId,
            Name = engineFunctionBlock.Name,
            RunIndex = engineFunctionBlock.RunIndex ?? uint.MaxValue,
            RunMode = engineFunctionBlock.RunMode,
            Id = engineFunctionBlock.Id,
        };
        workflow.FunctionBlocks.Add(functionBlock);

        foreach (var input in engineFunctionBlock.GetAllInputs())
        {
            if (input.EventEnabled)
            {
                var functionBlockDesign = dependencyResolver.ResolveFunctionBlockDesign(input.FunctionBlock.DesignId);
                var inputConnectorDesign = functionBlockDesign.GetInputs().First(x => x.Id == input.DesignId);
                brokerConnection.IncomingLinks.Add(new()
                {
                    ConnectorId = input.Id,
                    RemoteId = input.Id,
                    SourceType = DataTypeCompatibilityValidator.DetermineValueType(inputConnectorDesign.ConnectorType).AssemblyQualifiedName ?? string.Empty,
                });
            }

            var connector = TranslateConnector(input);
            connector.CrossSourcePoolingStrategy = input.CrossSourcePoolingStrategy;
            connector.CrossSourceAggregatingPooling = TypedObject.CreateOptional(input.CrossSourceAggregatingPooling);
            connector.PerSourcePoolingStrategy = input.PerSourcePoolingStrategy;
            connector.PerSourceAggregatingPooling = TypedObject.CreateOptional(input.PerSourceAggregatingPooling);
            foreach (var link in input.IncomingLinks)
            {
                // ignore links from untranslated DataPortTreeNodes to FunctionBlock connectors
                if (link.SourceConnector is null && link.SourceDataPortTreeNode is not null)
                    continue;
                // can happen if the referenced connector belongs to another application
                if (link.SourceConnector is null)
                    throw new InvalidOperationException($"Cannot determine source connector of link '{link.Id}' to '{input.Id}' ({input.Name}) of FunctionBlock '{functionBlock.Id}' ({functionBlock.Name}).");
                if (link.SourceConnector.FunctionBlock.EngineId == engineFunctionBlock.EngineId)
                {
                    TranslateInternalLink(workflow, link);
                }
                else
                {
                    var functionBlockDesign = dependencyResolver.ResolveFunctionBlockDesign(link.SourceConnector.FunctionBlock.DesignId);
                    var sourceType = functionBlockDesign.GetOutputs().FirstOrDefault(x => x.Id == link.SourceConnector.DesignId)?.ConnectorType;
                    if (sourceType is not null)
                        sourceType = DataTypeCompatibilityValidator.DetermineValueType(sourceType);
                    if (sourceType is null && !dataPortOutputDataTypes.TryGetValue(link.SourceConnector.Id, out sourceType))
                        throw new InvalidOperationException($"Cannot determine data type of source connector '{link.SourceConnector.Id}' ({link.SourceConnector.Name}) of link '{link.Id}' to '{input.Id}' ({input.Name}) of FunctionBlock '{functionBlock.Id}' ({functionBlock.Name}).");
                    var externalConnection = link.SourceConnector.FunctionBlock.Engine is not null &&
                                             host.Engines.Contains(link.SourceConnector.FunctionBlock.Engine)
                        ? directConnection
                        : brokerConnection;

                    externalConnection.IncomingLinks.Add(new()
                    {
                        ConnectorId = input.Id,
                        RemoteId = link.SourceConnector.Id,
                        SourceType = sourceType.AssemblyQualifiedName ?? string.Empty,
                        TypeConverter = TypedObject.CreateOptional(link.TypeConversion),
                    });
                }
            }
            functionBlock.Connectors.Add(connector);
        }

        foreach (var output in engineFunctionBlock.GetAllOutputs())
        {
            functionBlock.Connectors.Add(TranslateConnector(output));
            var sendDirectly = false;
            var sendToBroker = false;
            if (output.EventEnabled)
            {
                brokerConnection.OutgoingLinks.Add(new() { ConnectorId = output.Id, RemoteId = output.Id, });
                sendToBroker = true;
            }
            foreach (var link in output.Links.Where(link => link.DestinationConnector?.FunctionBlock.EngineId != engineFunctionBlock.EngineId))
            {
                ArgumentNullException.ThrowIfNull(link.DestinationConnector);
                if (!sendDirectly && link.DestinationConnector.FunctionBlock.Engine is not null && host.Engines.Contains(link.DestinationConnector.FunctionBlock.Engine))
                {
                    directConnection.OutgoingLinks.Add(new() { ConnectorId = output.Id, RemoteId = output.Id, });
                    sendDirectly = true;
                }
                else if (!sendToBroker)
                {
                    brokerConnection.OutgoingLinks.Add(new() { ConnectorId = output.Id, RemoteId = output.Id, });
                    sendToBroker = true;
                }
            }
        }

        foreach (var engineSetting in engineFunctionBlock.Settings)
            functionBlock.Settings.Add(TranslateSetting(engineSetting));

        foreach (var engineVariable in engineFunctionBlock.PersistentVariables)
            functionBlock.Variables.Add(TranslateVariable(engineVariable));
    }

    private static FunctionBlockConnector TranslateConnector(Connector engineConnector)
    {
        FunctionBlockConnector connector = new()
        {
            Id = engineConnector.Id,
            DesignId = engineConnector.DesignId,
            InitialValue = TypedObject.CreateOptional(engineConnector.Value),
            MarkAsChangedOnlyIfNotEqual = engineConnector.MarkAsChangedOnlyIfNotEqual,
            Validator = TypedObject.CreateOptional(engineConnector.Validators.FirstOrDefault()),
        };
        return connector;
    }

    private static void TranslateInternalLink(Workflow workflow, Link clusterLink)
    {
        ArgumentNullException.ThrowIfNull(clusterLink.SourceConnector);
        ArgumentNullException.ThrowIfNull(clusterLink.DestinationConnector);
        FunctionBlockConnectorLink link = new()
        {
            DestinationConnectorId = clusterLink.DestinationConnector.Id,
            SourceConnectorId = clusterLink.SourceConnector.Id,
            Id = clusterLink.Id,
            TypeConverter = TypedObject.CreateOptional(clusterLink.TypeConversion)
        };
        workflow.FunctionBlockConnectorLinks.Add(link);
    }

    private static FunctionBlockSetting TranslateSetting(Setting engineSetting) => new()
    {
        DesignId = engineSetting.DesignId,
        Timestamp = engineSetting.ValueChangedTime,
        Id = engineSetting.Id,
        Value = TypedObject.CreateOptional(engineSetting.Value),
    };

    private static FunctionBlockVariable TranslateVariable(PersistentVariable engineVariable) => new()
    {
        DesignId = engineVariable.DesignId,
        Timestamp = engineVariable.ValueChangedTime,
        Id = engineVariable.Id,
        Value = TypedObject.CreateOptional(engineVariable.Value),
    };

    private static void AddExternalCommunications(ExternalConnection directConnection, ExternalConnection internalConnection,
        IReadOnlyCollection<DataPortTransformation>? engineDataPorts, Workflow workflow, MqttCommunication brokerCommunication)
    {
        if (directConnection.IncomingLinks.Count > 0 || directConnection.OutgoingLinks.Count > 0)
        {
            directConnection.Communication = new() { Id = "718EECC8-2C38-432E-823B-0E46D64FEAF1", };
            workflow.ExternalConnections.Add(directConnection);
        }

        if (internalConnection.IncomingLinks.Count > 0 || internalConnection.OutgoingLinks.Count > 0)
        {
            internalConnection.Communication = brokerCommunication.ToCommunicationConfiguration();
            workflow.ExternalConnections.Add(internalConnection);
        }

        if (engineDataPorts is not null)
        {
            foreach (var engineDataPort in engineDataPorts)
            {
                if (engineDataPort.IncomingLinks.Count > 0)
                {
                    workflow.ExternalConnections.Add(new()
                    {
                        Name = engineDataPort.ConnectionName,
                        Communication = GetCommunicationConfiguration(engineDataPort, engineDataPort.IncomingNodes),
                        IncomingLinks = engineDataPort.IncomingLinks,
                    });
                }
                if (engineDataPort.OutgoingLinks.Count > 0)
                {
                    workflow.ExternalConnections.Add(new()
                    {
                        Name = engineDataPort.ConnectionName,
                        Communication = GetCommunicationConfiguration(engineDataPort, engineDataPort.OutgoingNodes),
                        OutgoingLinks = engineDataPort.OutgoingLinks,
                    });
                }
            }
        }

        static CommunicationConfiguration GetCommunicationConfiguration(DataPortTransformation transformation, IReadOnlyCollection<Node> nodes)
            => new()
            {
                Id = transformation.CommunicationId,
                Properties =
                [
                    new(nameof(IDataPortCommunication.Nodes)) { Value = TypedObject.CreateMandatory(nodes), },
                    .. transformation.CommunicationProperties
                ],
            };
    }

    private static void AddAdditionalConnectorDesigns(IReadOnlyCollection<DataPortTransformation>? engineDataPorts, Workflow workflow)
    {
        if (engineDataPorts is not null)
            workflow.AdditionalConnectorDesigns.AddRange(engineDataPorts.SelectMany(t => t.AdditionalConnectorDesigns));
    }
}

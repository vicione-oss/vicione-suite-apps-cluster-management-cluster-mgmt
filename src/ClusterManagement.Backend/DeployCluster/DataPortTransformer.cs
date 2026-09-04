using ClusterManagement.Backend.Engines;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Public.Extensions;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;
using ViciOne.Cluster.Model.Extensions;
using ViciOne.Suite.DataPort;
using ViciOne.Suite.System.Contracts;
using ViciOne.TreeBuilder;
using ViciOne.TreeBuilder.NodeTypes;
using DataPortTransferMode = ViciOne.Cluster.Model.DataPortTransferMode;
using Engine = ViciOne.Cluster.Model.Engine;
using FunctionBlock = ViciOne.Cluster.Model.FunctionBlock;
using GeneratedConnectorKey = (ViciOne.Cluster.Model.DataPortTreeNode Node, ViciOne.Cluster.Model.DataPortDirection Direction);

namespace ClusterManagement.Backend.DeployCluster;

internal sealed class DataPortTransformer
{
    private readonly DataPort _dataPort;
    private readonly ClusterBuilder _builder;
    private readonly ISuiteRulesetProvider _rulesetProvider;
    private readonly GuidFactory _guidFactory;
    private readonly Dictionary<GeneratedConnectorKey, ConnectorInput> _generatedInputs = [];
    private readonly Dictionary<GeneratedConnectorKey, ConnectorOutput> _generatedOutputs = [];
    private readonly List<NodeLink> _nodeLinks = [];
    private readonly Dictionary<Guid, OutputConfiguration> _outputConfigurations = [];
    private readonly Dictionary<Guid, ITransferMode> _transferModes = [];
    private readonly Dictionary<DataPortTreeNode, Node> _externalIncomingNodes = [];
    private readonly Dictionary<DataPortTreeNode, Node> _externalOutgoingNodes = [];
    private readonly List<IncomingLink> _externalIncomingLinks = [];
    private readonly List<OutgoingLink> _externalOutgoingLinks = [];
    private readonly List<FunctionBlockConnectorDesign> _additionalConnectorDesigns = [];

    internal static Guid DataPortFbDesignId => new("c7390e0f-761d-40f6-9112-a31216eac2c7");
    internal static Guid NodeLinksSettingId => new("a70d582d-7a17-4361-843f-299af86dcb97");
    internal static Guid OutputConfigurationsSettingId => new("a6b043fb-f21e-48b1-be5a-595adc18dc08");
    internal static Guid TransferModesSettingId => new("f73cc185-43ab-4b77-817a-eda924625b5b");

    private DataPortTransformer(DataPort dataPort, ClusterBuilder builder, ISuiteRulesetProvider rulesetProvider)
    {
        _dataPort = dataPort;
        _builder = builder;
        _rulesetProvider = rulesetProvider;
        _guidFactory = new(dataPort.Id);
    }

    internal static IDictionary<Engine, List<DataPortTransformation>> TransformToFunctionBlocks(
        ClusterBuilder builder, IReadOnlyCollection<EngineHost> engineHosts, ISuiteRulesetProvider rulesetProvider)
    {
        Dictionary<Engine, List<DataPortTransformation>> enginesDataPorts = [];
        foreach (var engineHost in engineHosts)
        {
            foreach (var engine in engineHost.Engines)
            {
                if (!builder.Cache.EngineDataPorts.TryGetValue(engine, out var dataPorts))
                    continue;

                foreach (var dataPort in dataPorts)
                {
                    var transformation = TransformToFunctionBlock(dataPort, builder, rulesetProvider);
                    builder.Editors.FunctionBlock.AssignEngine(engine, transformation.FunctionBlock);
                    if (!enginesDataPorts.TryGetValue(engine, out var engineDataPorts))
                        enginesDataPorts.Add(engine, engineDataPorts = []);
                    engineDataPorts.Add(transformation);
                }
            }
        }
        return enginesDataPorts;
    }

    internal static DataPortTransformation TransformToFunctionBlock(DataPort dataPort, ClusterBuilder builder, ISuiteRulesetProvider rulesetProvider)
        => new DataPortTransformer(dataPort, builder, rulesetProvider).Transform();

    private DataPortTransformation Transform()
    {
        var dataPortFb = CreateFunctionBlock();

        var inputNumber = 0;
        var outputNumber = 0;
        var ruleset = _rulesetProvider.GetRuleset(_dataPort.RulesetId);
        var treeBuilder = new TreeBuilder(ruleset);
        foreach (var (node, path) in _dataPort.Traverse())
        {
            var hasIncomingLinks = HasIncomingLinks(node);
            var hasOutgoingLinks = HasOutgoingLinks(node);

            CreateExternalCommunicationNodes(node, path, _dataPort.Direction, treeBuilder);

            foreach (var parent in path)
            {
                if (TransferIsPossible(parent))
                {
                    if (hasIncomingLinks)
                    {
                        var input = EnsureInput(dataPortFb, node, DataPortDirection.Out);
                        var output = CreateOutput(dataPortFb, node, DataPortDirection.Out, string.Concat(parent.Name, "_", ++outputNumber, "_", node.Name));
                        ConfigureTransferMode(input, output, parent);
                        ConfigureExternalOutgoingLink(output);
                        ConfigureExternalOutgoingCommunication(node, parent, output);
                    }

                    if (hasOutgoingLinks)
                    {
                        var input = CreateInput(dataPortFb, node, DataPortDirection.In, string.Concat(parent.Name, "_", ++inputNumber, "_", node.Name));
                        var output = CreateOutput(dataPortFb, node, DataPortDirection.In, string.Concat(parent.Name, "_", ++outputNumber, "_", node.Name));
                        DuplicateLinks(node, output);
                        ConfigureTransferMode(input, output, parent);
                        ConfigureExternalIncomingLink(input, node);
                        ConfigureExternalIncomingCommunication(node, parent, input);
                    }
                }
            }

            if (TransferIsPossible(node) && IsLeafNode(node))
            {
                if (hasIncomingLinks)
                {
                    var input = EnsureInput(dataPortFb, node, DataPortDirection.Out);
                    var output = EnsureOutput(dataPortFb, node, DataPortDirection.Out);
                    ConfigureTransferMode(input, output, node);
                    ConfigureExternalOutgoingLink(output);
                    ConfigureExternalOutgoingCommunication(node, output);
                }
                if (hasOutgoingLinks)
                {
                    var input = EnsureInput(dataPortFb, node, DataPortDirection.In);
                    var output = EnsureOutput(dataPortFb, node, DataPortDirection.In);
                    ConfigureTransferMode(input, output, node);
                    ConfigureExternalIncomingLink(input, node);
                    ConfigureExternalIncomingCommunication(node, input);
                }
            }
        }

        SetSetting(dataPortFb, NodeLinksSettingId, _nodeLinks);
        SetSetting(dataPortFb, OutputConfigurationsSettingId, _outputConfigurations);
        SetSetting(dataPortFb, TransferModesSettingId, _transferModes);

        return new()
        {
            FunctionBlock = dataPortFb,
            CommunicationId = _dataPort.RulesetId,
            CommunicationProperties = TransformProperties(_dataPort),
            ConnectionName = _dataPort.Name,
            IncomingLinks = _externalIncomingLinks,
            IncomingNodes = _externalIncomingLinks.Count == 0 ? [] : _externalIncomingNodes.Values,
            OutgoingLinks = _externalOutgoingLinks,
            OutgoingNodes = _externalOutgoingLinks.Count == 0 ? [] : _externalOutgoingNodes.Values,
            AdditionalConnectorDesigns = _additionalConnectorDesigns,
        };
    }

    private FunctionBlock CreateFunctionBlock()
    {
        var dataPortFb = _builder.Editors.Container.AddFunctionBlock(_dataPort.Dataflow.Root, DataPortFbDesignId, _dataPort.Name);
        dataPortFb.Id = _dataPort.Id;
        foreach (var defaultConnector in dataPortFb.GetConnectors())
            _builder.Editors.Connector.SetId(defaultConnector, _guidFactory.Next());
        foreach (var setting in dataPortFb.Settings)
            _builder.Editors.Setting.SetId(setting, _guidFactory.Next());
        foreach (var variable in dataPortFb.PersistentVariables)
            _builder.Editors.PersistentVariable.SetId(variable, _guidFactory.Next());
        if (_dataPort.Description is not null)
            _builder.Editors.FunctionBlock.SetDescription(dataPortFb, _dataPort.Description);
        return dataPortFb;
    }

    private static bool TransferIsPossible(DataPortTreeNode node)
        => node.TransferMode != DataPortTransferMode.None;

    private static bool IsLeafNode(DataPortTreeNode node) => node.Children.Count == 0;

    private bool HasIncomingLinks(DataPortTreeNode node)
        => GetIncomingLinks(node).Count != 0 && IncomingLinksAllowed();

    private List<Link> GetIncomingLinks(DataPortTreeNode node) => IncomingLinksAllowed() ? node.IncomingLinks : [];

    private bool IncomingLinksAllowed() => _dataPort.Direction is DataPortDirection.Out or DataPortDirection.InOut;

    private bool HasOutgoingLinks(DataPortTreeNode node)
        => GetOutgoingLinks(node).Any() && OutgoingLinksAllowed();

    private IEnumerable<Link> GetOutgoingLinks(DataPortTreeNode node)
        => OutgoingLinksAllowed() ? _builder.Cache.Links.Where(link => link.SourceDataPortTreeNodeId == node.Id) : [];

    private bool OutgoingLinksAllowed() => _dataPort.Direction is DataPortDirection.In or DataPortDirection.InOut;

    private static string ToValidClassName(Guid guid)
        => $"_{guid:n}";

    private ConnectorInput EnsureInput(FunctionBlock functionBlock, DataPortTreeNode node, DataPortDirection direction)
    {
        GeneratedConnectorKey key = (node, direction);
        if (!_generatedInputs.TryGetValue(key, out var input))
        {
            input = CreateInput(functionBlock, node, direction, node.Name);
            _generatedInputs.Add(key, input);

            if (direction == DataPortDirection.Out)
            {
                foreach (var link in GetIncomingLinks(node))
                {
                    link.DestinationConnector = input;
                    //link.DestinationDataPortTreeNode = null; // Zurücksetzen darf nicht ausgeführt werden, da sonst spätere Suchen fehlschlagen
                    input.IncomingLinks.Add(link);
                    if (link.SourceConnector is not null)
                        input.Value = link.SourceConnector.Value;
                }
            }
        }
        return input;
    }

    private ConnectorInput CreateInput(FunctionBlock functionBlock, DataPortTreeNode node, DataPortDirection direction, string name)
    {
        ConnectorInput input = new()
        {
            DesignId = _guidFactory.Next(),
            Id = _guidFactory.Next(),
            Name = $"{name}_{direction}",
            Description = node.Description,
            CrossSourcePoolingStrategy = node.TransferPooling is null
                ? ViciOne.Core.Contracts.DataModel.PoolingStrategy.Queue
                : ViciOne.Core.Contracts.DataModel.PoolingStrategy.Aggregate,
            CrossSourceAggregatingPooling = node.TransferPooling,
            PerSourcePoolingStrategy = node.TransferPooling is null
                ? ViciOne.Core.Contracts.DataModel.PoolingStrategy.Queue
                : ViciOne.Core.Contracts.DataModel.PoolingStrategy.Aggregate,
            PerSourceAggregatingPooling = node.TransferPooling,
            FunctionBlock = functionBlock,
        };
        functionBlock.Inputs.Add(input);

        _additionalConnectorDesigns.Add(new FunctionBlockConnectorDesign()
        {
            ConnectorId = input.Id,
            IsInput = true,
            Name = ToValidClassName(node.Id),
            ValueType = node.ValueType ?? throw new InvalidOperationException($"'{node.Id}' ({node.Name}) has no value data type to create a connector."),
        });

        return input;
    }

    private ConnectorOutput EnsureOutput(FunctionBlock functionBlock, DataPortTreeNode node, DataPortDirection direction)
    {
        GeneratedConnectorKey key = (node, direction);
        if (!_generatedOutputs.TryGetValue(key, out var output))
        {
            output = CreateOutput(functionBlock, node, direction, node.Name);
            _generatedOutputs.Add(key, output);

            if (direction == DataPortDirection.In)
            {
                foreach (var link in GetOutgoingLinks(node))
                {
                    link.SourceConnector = output;
                    link.SourceConnectorId = output.Id;
                    //link.SourceDataPortTreeNodeId = null; // Zurücksetzen darf nicht ausgeführt werden, da sonst spätere Suchen fehlschlagen
                    //link.SourceDataPortTreeNode = null;
                    output.Links.Add(link);
                    if (link.DestinationConnector is not null)
                        output.Value = link.DestinationConnector.Value;
                }
            }
        }
        return output;
    }

    private ConnectorOutput CreateOutput(FunctionBlock functionBlock, DataPortTreeNode node, DataPortDirection direction, string name)
    {
        ConnectorOutput output = new()
        {
            DesignId = _guidFactory.Next(),
            Id = _guidFactory.Next(),
            Name = $"{name}_{direction}",
            Description = node.Description,
            FunctionBlock = functionBlock,
        };
        functionBlock.Outputs.Add(output);

        _additionalConnectorDesigns.Add(new FunctionBlockConnectorDesign()
        {
            ConnectorId = output.Id,
            IsInput = false,
            Name = ToValidClassName(node.Id),
            ValueType = node.ValueType ?? throw new InvalidOperationException($"'{node.Id}' ({node.Name}) has no value data type to create a connector."),
        });

        _outputConfigurations[output.Id] = new()
        {
            Pooling = new Pooling
            {
                CrossSourcePoolingStrategy = node.TransferPooling is null
                    ? ViciOne.Core.Contracts.DataModel.PoolingStrategy.Queue
                    : ViciOne.Core.Contracts.DataModel.PoolingStrategy.Aggregate,
                CrossSourceAggregatingPooling = node.TransferPooling,
                PerSourcePoolingStrategy = node.TransferPooling is null
                    ? ViciOne.Core.Contracts.DataModel.PoolingStrategy.Queue
                    : ViciOne.Core.Contracts.DataModel.PoolingStrategy.Aggregate,
                PerSourceAggregatingPooling = node.TransferPooling,
            },
            MarkAsChangedIfNotEqual = false,
        };

        return output;
    }

    private void DuplicateLinks(DataPortTreeNode node, ConnectorOutput output)
    {
        foreach (var link in GetOutgoingLinks(node))
        {
            if (link.DestinationConnector is not null)
            {
                Link newLink = new()
                {
                    Id = _guidFactory.Next(),
                    SourceConnector = output,
                    SourceConnectorId = output.Id,
                    DestinationConnector = link.DestinationConnector,
                    TypeConversion = link.TypeConversion,
                    Visible = true,
                };
                output.Links.Add(newLink);
                link.DestinationConnector.Links.Add(newLink);
                output.Value = link.DestinationConnector.Value;
            }
        }
    }

    private void ConfigureTransferMode(ConnectorInput input, ConnectorOutput output, DataPortTreeNode node)
    {
        _nodeLinks.Add(new()
        {
            InputConnectorId = input.Id,
            OutputConnectorId = output.Id,
            PropertyPath = default,
            Modification = default,
        });

        _transferModes[output.Id] = node.TransferMode switch
        {
            DataPortTransferMode.OnChange => new OnChangedTransferMode(),
            DataPortTransferMode.Periodic => new PeriodicTransferMode(TimeSpan.FromMilliseconds(node.TransferIntervalInMs ??
                throw new InvalidOperationException($"The DataPort tree node '{node.Id}' ({node.Name}) needs an valid interval to use a periodic transfer mode."))),
            DataPortTransferMode.None => throw new InvalidOperationException($"The DataPort tree node '{node.Id}' ({node.Name}) has an unusable '{node.TransferMode}'."),
            _ => throw new NotSupportedException($"The DataPort tree node '{node.Id}' ({node.Name}) has an unsupported transfer mode '{node.TransferMode}'."),
        };
    }

    private void ConfigureExternalIncomingLink(ConnectorInput input, DataPortTreeNode node)
        => _externalIncomingLinks.Add(new() { ConnectorId = input.Id, RemoteId = input.Id, SourceType = node.ValueType?.AssemblyQualifiedName ?? string.Empty, });

    private void ConfigureExternalOutgoingLink(ConnectorOutput output)
        => _externalOutgoingLinks.Add(new() { ConnectorId = output.Id, RemoteId = output.Id, });

    private void ConfigureExternalIncomingCommunication(DataPortTreeNode node, ConnectorInput nodeConnector)
        => ConfigureExternalCommunication(_externalIncomingNodes, node, nodeConnector);

    private void ConfigureExternalOutgoingCommunication(DataPortTreeNode node, ConnectorOutput nodeConnector)
        => ConfigureExternalCommunication(_externalOutgoingNodes, node, nodeConnector);

    private static void ConfigureExternalCommunication(Dictionary<DataPortTreeNode, Node> externalCommunicationNodes,
        DataPortTreeNode node, Connector nodeConnector)
    {
        var externalNode = externalCommunicationNodes[node];
        EnsureContains(externalNode.AffectedChannels, nodeConnector.Id.ToString());
        EnsureContains(externalNode.TransferredChannels, nodeConnector.Id.ToString());
    }

    private void ConfigureExternalIncomingCommunication(DataPortTreeNode leaf, DataPortTreeNode group, Connector groupLeafConnector)
        => ConfigureExternalCommunication(_externalIncomingNodes, leaf, group, groupLeafConnector);

    private void ConfigureExternalOutgoingCommunication(DataPortTreeNode leaf, DataPortTreeNode group, Connector groupLeafConnector)
        => ConfigureExternalCommunication(_externalOutgoingNodes, leaf, group, groupLeafConnector);

    private static void ConfigureExternalCommunication(Dictionary<DataPortTreeNode, Node> externalCommunicationNodes,
        DataPortTreeNode leaf, DataPortTreeNode group, Connector groupLeafConnector)
    {
        var externalLeaf = externalCommunicationNodes[leaf];
        EnsureContains(externalLeaf.AffectedChannels, groupLeafConnector.Id.ToString());
        var externalGroup = externalCommunicationNodes[group];
        EnsureContains(externalGroup.TransferredChannels, groupLeafConnector.Id.ToString());
    }

    private static void EnsureContains(List<string> values, string value)
    {
        if (!values.Contains(value))
            values.Add(value);
    }

    private void CreateExternalCommunicationNodes(DataPortTreeNode node, IReadOnlyCollection<DataPortTreeNode> path,
        DataPortDirection direction, TreeBuilder treeBuilder)
    {
        if (path.Any(TransferIsPossible) || TransferIsPossible(node))
        {
            Node? incomingTreeParent = default;
            Node? outgoingTreeParent = default;
            foreach (var treeNode in path.Append(node))
            {
                if (direction is DataPortDirection.Out or DataPortDirection.InOut)
                    incomingTreeParent = CreateExternalCommunicationNodes(treeNode, incomingTreeParent, _externalOutgoingNodes, treeBuilder);
                if (direction is DataPortDirection.In or DataPortDirection.InOut)
                    outgoingTreeParent = CreateExternalCommunicationNodes(treeNode, outgoingTreeParent, _externalIncomingNodes, treeBuilder);
            }
        }
    }

    private static Node CreateExternalCommunicationNodes(DataPortTreeNode node, Node? parent, Dictionary<DataPortTreeNode, Node> externalCommunicationNodes, TreeBuilder treeBuilder)
    {
        if (!externalCommunicationNodes.TryGetValue(node, out var externalCommunicationNode))
        {
            externalCommunicationNode = CreateExternalCommunicationNode(node, treeBuilder, parent);
            externalCommunicationNodes.Add(node, externalCommunicationNode);
        }
        return externalCommunicationNode;
    }

    private static Node CreateExternalCommunicationNode(DataPortTreeNode node, TreeBuilder treeBuilder, Node? parent = default) => new()
    {
        Id = node.Id,
        DesignId = GetMappingId(treeBuilder, node.DesignId),
        Name = node.Name,
        ParentId = parent?.Id,
        ValueType = node.ValueType,
        Properties = GetExternalCommunicationNodeProperties(node),
    };

    private static string GetMappingId(TreeBuilder treeBuilder, string dataPortNodeDesignId)
        => treeBuilder.NodeTypes.TryGetValue(dataPortNodeDesignId, out var nodeType) && nodeType is DataPortTreeNodeType dpNodeType
            ? dpNodeType.MappingId
            : throw new InvalidOperationException($"DataPort tree node design id '{dataPortNodeDesignId}' could not be resolved to a {nameof(DataPortTreeNodeType)}.");

    private static Dictionary<string, Property> GetExternalCommunicationNodeProperties(DataPortTreeNode node)
    {
        Dictionary<string, Property> properties = [];
        foreach (var nodeProperty in node.Properties)
        {
            if (nodeProperty.Value is not null)
                properties.Add(nodeProperty.DesignId, new() { Value = nodeProperty.Value, });
        }
        return properties;
    }

    private void SetSetting(FunctionBlock functionBlock, Guid settingId, object? value)
    {
        var setting = functionBlock.Settings.First(s => s.DesignId == settingId);
        var time = setting.ValueChangedTime;
        _builder.Editors.Setting.SetValue(setting, value);
        setting.ValueChangedTime = time;
    }

    private static IReadOnlyCollection<CommunicationProperty> TransformProperties(DataPort dataPort)
        => [.. dataPort.Properties.Select(p => new CommunicationProperty(p.DesignId) { Value = TypedObject.CreateOptional(p.Value), }),
            new CommunicationProperty(nameof(IDataPortCommunication.Name)) { Value = TypedObject.CreateMandatory(dataPort.Name), },
            new CommunicationProperty(nameof(IDataPortCommunication.DesignId)) { Value = TypedObject.CreateMandatory(dataPort.DesignId) }];
}

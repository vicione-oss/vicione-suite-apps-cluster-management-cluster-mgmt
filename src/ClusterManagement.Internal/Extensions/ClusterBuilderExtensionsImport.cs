using System.Drawing;
using ClusterManagement.Internal.Contracts;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;
using ViciOne.Cluster.Model.Extensions;

namespace ClusterManagement.Internal.Extensions;

public static class ClusterBuilderExtensionsImport
{
    public static ClusterImportResult ImportCluster(this ClusterBuilder builder, Cluster other, ClusterImportOptions options)
    {
        // !! if working with the Cluster directly be aware of JSONIgnore properties won't be initialized
        var result = new ClusterImportResult();

        // add missing dependencies or use higher version
        builder.MigrateClusterDependencies(other, result.Dependencies);

        // add missing fb designs
        builder.MigrateFunctionBlockDesigns(other, result.FunctionBlockDesigns);

        // topology: nodegroup -> nodegroup|node (instanceid) -> app -> enginehost -> engines
        builder.MigrateNodeGroups(other.NodeGroups, options, result);

        foreach (var dataflow in other.Dataflows)
        {
            try
            {
                var copy = builder.GetOrCreateDataflow(dataflow, result.Dataflows);

                // container -> container|functionblock -> connector
                builder.MigrateContainers(copy.Root, dataflow.Root, options, result);

                // added missing DataPorts, DataPortTreeNodes, Properties - only root container has DataPorts by convention
                builder.MigrateDataPorts(copy, dataflow.DataPorts, options, result);
            }
            catch (Exception e)
            {
                result.Dataflows.Errors.Add(e.Message);
            }
        }

        // add all missing links (Connectors, ContainerConnectors, DataPortTreeNodes)
        builder.MigrateLinks(other, result.Links);

        return result;
    }

    private static void MigrateClusterDependencies(this ClusterBuilder builder, Cluster other, ClusterImportResultVersionItem resultItem)
    {
        foreach (var dependency in other.Dependencies)
        {
            // we have already exactly the same dependency 
            if (builder.Cache.ClusterDependencies.Contains(dependency))
            {
                resultItem.Skipped++;
                continue;
            }

            var existingVersion = builder.Cache.ClusterDependencies.FirstOrDefault(k => k.Name == dependency.Name);
            if (existingVersion is not null)
            {
                // import cluster has a higher version - should we support it?!
                if (existingVersion.Version < dependency.Version)
                {
                    resultItem.Errors.Add($"Cluster dependency {dependency.Name} downgrade from {dependency.Version} to {existingVersion.Version} is not supported.");
                    resultItem.Skipped++;
                    continue;
                }

                // we skip it but track the version upgrades
                resultItem.Versions.Add(new ClusterImportResultVersion
                {
                    ImportVersion = dependency.Version,
                    CurrentVersion = existingVersion.Version,
                    PackageName = dependency.Name
                });

                resultItem.Skipped++;
                continue;
            }

            // add missing dependency
            TryAndLog(() =>
            {
                builder.Editors.Cluster.AddDependency(dependency.Name, dependency.Version);
                resultItem.Added++;
            }, resultItem);
        }
    }

    private static void MigrateFunctionBlockDesigns(this ClusterBuilder builder, Cluster other, ClusterImportResultItem resultItem)
    {
        foreach (var dataflow in other.Dataflows)
        {
            foreach (var designId in dataflow.Root.GetAllNestedFunctionBlocks().Select(k => k.DesignId).Distinct())
            {
                if (builder.Cache.FunctionBlockDesigns.ContainsKey(designId))
                {
                    resultItem.Skipped++;
                    continue;
                }

                TryAndLog(() =>
                {
                    builder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(designId);
                    resultItem.Added++;
                }, resultItem);
            }
        }
    }

    private static void MigrateNodeGroups(this ClusterBuilder builder, IList<ClusterNodeGroup> nodeGroups, ClusterImportOptions options, ClusterImportResult result)
    {
        foreach (var nodeGroup in nodeGroups)
        {
            builder.MigrateNodeGroup(nodeGroup, options, result);

            if (nodeGroup.NodeGroups.Count == 0)
                continue;

            builder.MigrateNodeGroups(nodeGroup.NodeGroups, options, result);
        }
    }

    private static void MigrateNodeGroup(this ClusterBuilder builder, ClusterNodeGroup other, ClusterImportOptions options, ClusterImportResult result)
    {
        var nodeGroup = builder.GetOrCreateNodeGroup(other, options, result.NodeGroups);

        foreach (var node in other.Nodes)
        {
            builder.MigrateNode(nodeGroup, node, options, result);
        }
    }

    private static void MigrateNode(this ClusterBuilder builder, ClusterNodeGroup nodeGroup, ClusterNode other, ClusterImportOptions options, ClusterImportResult result)
    {
        var node = builder.GetOrCreateNode(nodeGroup, other, options, result.Nodes);

        foreach (var application in other.Applications)
        {
            builder.MigrateApplication(node, application, options, result);
        }
    }

    private static void MigrateApplication(this ClusterBuilder builder, ClusterNode node, ClusterApplication other, ClusterImportOptions options, ClusterImportResult result)
    {
        var application = builder.GetOrCreateApplication(node, other, options, result.Applications);

        foreach (var engineHost in other.EngineHosts)
        {
            builder.MigrateEngineHost(application, engineHost, options, result);
        }
    }

    private static void MigrateEngineHost(this ClusterBuilder builder, ClusterApplication application, EngineHost other, ClusterImportOptions options, ClusterImportResult result)
    {
        var engineHost = builder.GetOrCreateEngineHost(application, other, options, result.EngineHosts);

        foreach (var engine in other.Engines)
        {
            builder.GetOrCreateEngine(engineHost, engine, result.Engines);
        }
    }

    private static ClusterNodeGroup GetOrCreateNodeGroup(this ClusterBuilder builder, ClusterNodeGroup nodeGroup, ClusterImportOptions options, ClusterImportResultItem resultItem)
    {
        var cached = builder.Cache.ClusterNodeGroups.FirstOrDefault(k => k.Id == nodeGroup.Id);
        if (cached is not null)
        {
            resultItem.Skipped++;
            return cached;
        }

        if (options.MergeNodeGroups)
        {
            var defaultGroup = builder.Cache.ClusterNodeGroups.FirstOrDefault();
            if (defaultGroup != null)
            {
                resultItem.Merged++;
                return defaultGroup;
            }
        }

        resultItem.Added++;
        var created = builder.Editors.Cluster.AddNodeGroup(nodeGroup.Name, nodeGroup.Id, nodeGroup.NetworkType);

        builder.Editors.NodeGroup.SetDescription(created, nodeGroup.Description);
        builder.Editors.NodeGroup.SetIcon(created, nodeGroup.Icon);

        return created;
    }

    private static ClusterNode GetOrCreateNode(this ClusterBuilder builder, ClusterNodeGroup nodeGroup, ClusterNode node, ClusterImportOptions options, ClusterImportResultItem resultItem)
    {
        var cached = builder.Cache.ClusterNodes.FirstOrDefault(k => k.Id == node.Id);
        if (cached is not null)
        {
            resultItem.Skipped++;
            return cached;
        }

        if (options.MergeNodes)
        {
            var defaultNode = builder.Cache.ClusterNodes.FirstOrDefault();
            if (defaultNode != null)
            {
                resultItem.Merged++;
                return defaultNode;
            }
        }

        resultItem.Added++;
        var created = builder.Editors.NodeGroup.AddNode(nodeGroup, node.Name, node.Id, node.Type);

        builder.Editors.Node.SetDescription(created, node.Description);
        builder.Editors.Node.SetIcon(created, node.Icon);
        builder.Editors.Node.SetComputePowerLevel(created, node.ComputePowerLevel);

        return created;
    }

    private static ClusterApplication GetOrCreateApplication(this ClusterBuilder builder, ClusterNode node, ClusterApplication application, ClusterImportOptions options, ClusterImportResultItem resultItem)
    {
        var cached = builder.Cache.ClusterApplications.FirstOrDefault(k => k.Id == application.Id);
        if (cached is not null)
        {
            resultItem.Skipped++;
            return cached;
        }

        if (options.MergeApplications)
        {
            var defaultApplication = builder.Cache.ClusterApplications.FirstOrDefault();
            if (defaultApplication != null)
            {
                resultItem.Merged++;
                return defaultApplication;
            }
        }

        resultItem.Added++;
        var created = builder.Editors.Node.AddApplication(node, application.Type, application.Name, application.Id);

        builder.Editors.Application.SetDescription(created, application.Description);
        builder.Editors.Application.SetIcon(created, application.Icon);
        builder.Editors.Application.SetReplicas(created, application.Replicas);
        builder.Editors.Application.SetScaleDownCpuLoad(created, application.ScaleDownCpuLoad);
        builder.Editors.Application.SetScaleUpCpuLoad(created, application.ScaleUpCpuLoad);

        return created;
    }

    private static EngineHost GetOrCreateEngineHost(this ClusterBuilder builder, ClusterApplication application, EngineHost engineHost, ClusterImportOptions options, ClusterImportResultItem resultItem)
    {
        var cached = builder.Cache.EngineHosts.FirstOrDefault(k => k.Id == engineHost.Id);
        if (cached is not null)
        {
            resultItem.Skipped++;
            return cached;
        }

        if (options.MergeEngineHosts)
        {
            var defaultEngineHost = builder.Cache.EngineHosts.FirstOrDefault();
            if (defaultEngineHost != null)
            {
                resultItem.Merged++;
                return defaultEngineHost;
            }
        }

        resultItem.Added++;
        var created = builder.Editors.Application.AddEngineHost(application, engineHost.Name, engineHost.Id);

        builder.Editors.EngineHost.SetDescription(created, engineHost.Description);
        builder.Editors.EngineHost.SetIcon(created, engineHost.Icon);
        builder.Editors.EngineHost.SetLogLevel(created, engineHost.LogLevel);
        builder.Editors.EngineHost.SetReplicas(created, engineHost.Replicas);

        return created;
    }

    private static Engine GetOrCreateEngine(this ClusterBuilder builder, EngineHost engineHost, Engine engine, ClusterImportResultItem resultItem)
    {
        if (builder.Cache.EngineGuids.TryGetValue(engine.Id, out var cached))
        {
            resultItem.Skipped++;
            return cached;
        }

        resultItem.Added++;
        var created = builder.Editors.EngineHost.AddEngine(engineHost, engine.Name, engine.Id, engine.EngineType);

        builder.Editors.Engine.SetDescription(created, engine.Description);
        builder.Editors.Engine.SetEnabled(created, engine.Enabled);
        builder.Editors.Engine.SetIcon(created, engine.Icon);
        builder.Editors.Engine.SetLogLevel(created, engine.LogLevel);
        builder.Editors.Engine.SetMaxCycleTime(created, engine.MaxCycleTime);
        builder.Editors.Engine.SetMinCycleTime(created, engine.MinCycleTime);

        return created;
    }

    private static void MigrateContainers(this ClusterBuilder builder, Container target, Container other, ClusterImportOptions options, ClusterImportResult result)
    {
        // adding fbs will create their connectors
        foreach (var functionBlock in other.FunctionBlocks)
        {
            TryAndLog(() => builder.GetOrCreateFunctionBlock(target, functionBlock, result.FunctionBlocks), result.FunctionBlocks);
        }

        foreach (var label in other.Labels)
        {
            TryAndLog(() => builder.GetOrCreateLabel(target, label, result.Labels), result.Labels);
        }

        // create other root child containers in builder.Dataflow (root)
        foreach (var container in other.Containers)
        {
            TryAndLog(() => builder.MigrateChildContainer(target, container, options, result), result.Containers);
        }
    }

    private static void MigrateChildContainer(this ClusterBuilder builder, Container parent, ChildContainer other, ClusterImportOptions options, ClusterImportResult result)
    {
        var migContainer = builder.GetOrCreateContainer(parent, other, result.Containers);

        // adding fbs will create their connectors - these will have new ids (links lost!?!)
        foreach (var functionBlock in other.FunctionBlocks)
        {
            TryAndLog(() => builder.GetOrCreateFunctionBlock(migContainer, functionBlock, result.FunctionBlocks), result.FunctionBlocks);
        }

        foreach (var label in other.Labels)
        {
            TryAndLog(() => builder.GetOrCreateLabel(migContainer, label, result.Labels), result.Labels);
        }

        foreach (var input in other.ProcessDataInputs)
        {
            TryAndLog(() => builder.GetOrCreateContainerConnector(input, result.Labels), result.ContainerConnectors);
        }

        foreach (var output in other.ProcessDataOutputs)
        {
            TryAndLog(() => builder.GetOrCreateContainerConnector(output, result.Labels), result.ContainerConnectors);
        }

        // handle the children
        foreach (var child in other.Containers)
        {
            TryAndLog(() => builder.MigrateChildContainer(migContainer, child, options, result), result.Containers);
        }
    }

    private static void GetOrCreateContainerConnector(this ClusterBuilder builder, ContainerConnector other, ClusterImportResultItem resultItem)
    {
        var existing = builder.Cache.ContainerConnectors.FirstOrDefault(k => k.Id == other.Id);
        if (existing is not null || !builder.Cache.ConnectorGuids.TryGetValue(other.ConnectorId, out var connector))
        {
            resultItem.Skipped++;
            return;
        }

        TryAndLog(() =>
        {
            var created = builder.Editors.Connector.AddToParentContainer(connector).First();

            // set back the id from other
            builder.Editors.Connector.SetId(created, other.Id);
            resultItem.Added++;
        }, resultItem);
    }

    private static Dataflow GetOrCreateDataflow(this ClusterBuilder builder, Dataflow other, ClusterImportResultItem resultItem)
    {
        var existing = builder.Cache.Dataflows.FirstOrDefault(k => k.Id == other.Id);
        if (existing is not null)
        {
            resultItem.Skipped++;
            return existing;
        }

        resultItem.Added++;
        var created = builder.Editors.Cluster.AddDataflow(other.Name, other.Version, other.Id);

        builder.Editors.Dataflow.SetDescription(created, other.Description);
        builder.Editors.Dataflow.SetIcon(created, other.Icon);

        return created;
    }

    private static Container GetOrCreateContainer(this ClusterBuilder builder, Container parent, ChildContainer other, ClusterImportResultItem resultItem)
    {
        if (builder.Cache.ContainerGuids.TryGetValue(other.Id, out var existing))
        {
            resultItem.Skipped++;
            return existing;
        }

        resultItem.Added++;
        var created = builder.Editors.Container.AddContainer(parent, other.Name, other.Id, new(other.X ?? 0, other.Y ?? 0));

        builder.Editors.Container.SetDescription(created, other.Description);
        builder.Editors.Container.SetZoom(created, other.Zoom);
        builder.Editors.Container.SetBackColor(created, other.BackColor);
        builder.Editors.Container.SetForeColor(created, other.ForeColor);
        builder.Editors.Container.SetReadOnly(created, other.ReadOnly);
        builder.Editors.Container.SetViewport(created, new PointF(Convert.ToSingle(other.ViewportX ?? 0), Convert.ToSingle(other.ViewportY ?? 0)));

        return created;
    }

    private static FunctionBlock GetOrCreateFunctionBlock(this ClusterBuilder builder, Container container, FunctionBlock functionBlock, ClusterImportResultItem resultItem)
    {
        if (builder.Cache.FunctionBlockGuids.TryGetValue(functionBlock.Id, out var existing))
        {
            resultItem.Skipped++;
            return existing;
        }

        var location = new Point(functionBlock.X ?? 0, functionBlock.Y ?? 0);
        var created = builder.Editors.Container.AddFunctionBlock(container, functionBlock.DesignId, functionBlock.Name, functionBlock.Id, location);

        builder.Editors.FunctionBlock.SetCycleFrequency(created, functionBlock.CycleFrequency);
        builder.Editors.FunctionBlock.SetDescription(created, functionBlock.Description);
        builder.Editors.FunctionBlock.SetRunMode(created, functionBlock.RunMode);
        builder.Editors.FunctionBlock.SetBackColor(created, functionBlock.BackColor);
        builder.Editors.FunctionBlock.SetForeColor(created, functionBlock.ForeColor);

        // write back the imported connector ids
        var importInputs = functionBlock.GetAllInputs().ToList();
        foreach (var item in created.GetAllInputs())
        {
            builder.Editors.Connector.SetId(item, importInputs.First(k => k.DesignId == item.DesignId).Id);
        }

        var importOutput = functionBlock.GetAllOutputs().ToList();
        foreach (var item in created.GetAllOutputs())
        {
            builder.Editors.Connector.SetId(item, importOutput.First(k => k.DesignId == item.DesignId).Id);
        }

        // engine is already created so we can assign it
        if (functionBlock.EngineId.HasValue && builder.Cache.EngineGuids.TryGetValue(functionBlock.EngineId.Value, out var engine))
        {
            builder.Editors.FunctionBlock.AssignEngine(engine, created);
        }

        resultItem.Added++;

        return created;
    }

    private static void MigrateDataPorts(this ClusterBuilder builder, Dataflow dataflow, List<DataPort> dataPorts, ClusterImportOptions options, ClusterImportResult result)
    {
        foreach (var dataPort in dataPorts)
        {
            TryAndLog(() => builder.GetOrCreateDataPort(dataflow, dataPort, options, result), result.DataPorts);
        }
    }

    private static DataPort GetOrCreateDataPort(this ClusterBuilder builder, Dataflow dataflow, DataPort dataPort, ClusterImportOptions options, ClusterImportResult result)
    {
        if (builder.Cache.DataPortGuids.TryGetValue(dataPort.Id, out var existing))
        {
            // we don't touch the DataPortTreeNodes of existing DataPort but we could...
            result.DataPorts.Skipped++;
            return existing;
        }

        result.DataPorts.Added++;
        var addedDataPort = builder.Editors.Dataflow.AddDataPort(dataflow, dataPort.DesignId, dataPort.Name, dataPort.Direction, dataPort.RulesetId, dataPort.Id);

        builder.Editors.DataPort.SetDescription(addedDataPort, dataPort.Description);
        builder.Editors.DataPort.SetIcon(addedDataPort, dataPort.Icon);

        if (dataPort.EngineId is not null && builder.Cache.EngineGuids.TryGetValue(dataPort.EngineId.Value, out var engine))
        {
            builder.Editors.DataPort.AssignEngine(engine, addedDataPort);
        }

        // we add only not existing properties - we could also update existing
        foreach (var property in dataPort.Properties)
        {
            if (builder.Cache.DataPortPropertyGuids.ContainsKey(property.Id))
            {
                result.DataPortProperties.Skipped++;
                continue;
            }

            TryAndLog(() =>
            {
                builder.Editors.DataPort.AddProperty(property.DesignId, addedDataPort, property.Id, property.Value);
                result.DataPortProperties.Added++;
            }, result.DataPortProperties);
        }

        // we add not existing DataPortTreeNodes - links will be added after everything else is added
        foreach (var rootNode in dataPort.TreeNodes)
        {
            if (builder.Cache.DataPortTreeNodeGuids.ContainsKey(rootNode.Id))
            {
                result.DataPortTreeNodes.Skipped++;
                continue;
            }

            // e.g. for MqttBroker it could be the first DataPoint node but the ValueType is not initialized without builder -> GetValueType
            var createdTreeNode = builder.Editors.DataPort.AddTreeNode(rootNode.DesignId, addedDataPort, rootNode.Name, rootNode.ValueType, rootNode.TransferMode, rootNode.Id);
            result.DataPortTreeNodes.Added++;

            builder.MigrateDataPortTreeNodes(createdTreeNode, rootNode.Children, options, result);
        }

        return addedDataPort;
    }

    private static void MigrateDataPortTreeNodes(this ClusterBuilder builder, DataPortTreeNode targetParent, IList<DataPortTreeNode> nodes, ClusterImportOptions options, ClusterImportResult result)
    {
        foreach (var node in nodes)
        {
            var created = builder.GetOrCreateDataPortTreeNode(targetParent, node, result);

            if (node.Children.Count == 0)
                continue;

            builder.MigrateDataPortTreeNodes(created, node.Children, options, result);
        }
    }

    private static DataPortTreeNode GetOrCreateDataPortTreeNode(this ClusterBuilder builder, DataPortTreeNode targetParent, DataPortTreeNode node, ClusterImportResult result)
    {
        if (builder.Cache.DataPortTreeNodeGuids.TryGetValue(node.Id, out var existing))
        {
            result.DataPortTreeNodes.Skipped++;
            return existing;
        }

        // todo check if this is valid!
        // var valueType = node.ValueTypeSerialized is not null ? Type.GetType(node.ValueTypeSerialized, true)! : null;
        var created = builder.Editors.DataPortTreeNode.AddTreeNode(node.DesignId, targetParent, node.Name, node.ValueType, node.TransferMode, node.Id);

        builder.Editors.DataPortTreeNode.SetDescription(created, node.Description);
        builder.Editors.DataPortTreeNode.SetIcon(created, node.Icon);
        builder.Editors.DataPortTreeNode.SetTransferIntervalInMs(created, node.TransferIntervalInMs);
        builder.Editors.DataPortTreeNode.SetTransferPooling(created, node.TransferPooling);
        builder.Editors.DataPortTreeNode.SetValueType(created, node.ValueType);

        foreach (var property in node.Properties)
        {
            TryAndLog(() => builder.AddOrSkipDataPortProperty(created, property, result.DataPortProperties), result.DataPortProperties);
        }

        result.DataPortTreeNodes.Added++;
        return created;
    }

    private static void AddOrSkipDataPortProperty(this ClusterBuilder builder, DataPortTreeNode node, DataPortProperty property, ClusterImportResultItem resultItem)
    {
        if (builder.Cache.DataPortPropertyGuids.ContainsKey(property.Id))
        {
            resultItem.Skipped++;
            return;
        }

        builder.Editors.DataPortTreeNode.AddProperty(property.DesignId, node, property.Id, property.Value);
        resultItem.Added++;
    }

    private static Label GetOrCreateLabel(this ClusterBuilder builder, Container container, Label label, ClusterImportResultItem resultItem)
    {
        if (builder.Cache.LabelGuids.TryGetValue(label.Id, out var existing))
        {
            resultItem.Skipped++;
            return existing;
        }

        var created = builder.Editors.Container.AddLabel(
            container,
            new Point(label.X, label.Y),
            new Size(label.Width, label.Height),
            label.ZIndex,
            label.Content,
            label.Id);

        builder.Editors.Label.SetBackColor(created, label.BackColor);
        builder.Editors.Label.SetBorderColor(created, label.BorderColor);

        resultItem.Added++;
        return created;
    }

    private static void MigrateLinks(this ClusterBuilder builder, Cluster other, ClusterImportResultItem resultItem)
    {
        foreach (var dataflow in other.Dataflows)
        {
            // ContainerConnectors need to be handled first
            var containerInputs = dataflow.Root.GetAllNestedContainers().SelectMany(k => k.ProcessDataInputs);

            foreach (var item in containerInputs)
            {
                var containerConnectors = dataflow.Root.GetAllNestedFunctionBlocks().SelectMany(k => k.Inputs).Where(i => i.Id == item.ConnectorId);
                builder.MigrateContainerConnectorLinks(item, containerConnectors, resultItem);
            }

            var containerOutputs = dataflow.Root.GetAllNestedContainers().SelectMany(k => k.ProcessDataOutputs);
            foreach (var item in containerOutputs)
            {
                var containerConnectors = dataflow.Root.GetAllNestedFunctionBlocks().SelectMany(k => k.Outputs).Where(i => i.Id == item.ConnectorId);
                builder.MigrateContainerConnectorLinks(item, containerConnectors, resultItem);
            }

            // inputs receive data either from outputs or dataport tree nodes
            var inputs = dataflow.Root.GetAllNestedFunctionBlocks().SelectMany(k => k.Inputs).Where(i => i.Links.Count > 0);
            foreach (var input in inputs)
            {
                builder.MigrateConnectorLinks(input, resultItem);
            }
        }

        // dataport tree nodes can have links to IN|OUT connectors or container connectors
        foreach (var treeNode in other.GetAllNestedDataPortTreeNodes())
        {
            builder.MigrateDataPortTreeNodeLink(treeNode, resultItem);
        }
    }

    private static void MigrateContainerConnectorLinks(this ClusterBuilder builder, ContainerConnector other, IEnumerable<Connector> otherConnectors, ClusterImportResultItem resultItem)
    {
        var cacheContainerConnector = builder.Cache.ContainerConnectors.FirstOrDefault(k => k.Id == other.Id);
        if (cacheContainerConnector == null)
            return;

        foreach (var connector in otherConnectors)
        {
            if (!builder.Cache.ConnectorGuids.TryGetValue(other.ConnectorId, out var cacheConnector))
                continue;

            foreach (var link in connector.Links)
            {
                if (builder.Cache.LinkGuids.ContainsKey(link.Id))
                {
                    resultItem.Skipped++;
                    continue;
                }

                if (cacheConnector is IConnectorInput && cacheContainerConnector is ContainerConnectorInput containerInput)
                {
                    if (link.SourceConnectorId.HasValue && builder.Cache.ConnectorGuids.TryGetValue(link.SourceConnectorId.Value, out var srcConnector))
                    {
                        TryAndLog(() =>
                        {
                            builder.Editors.Connector.AddLink((IConnectorOutput)srcConnector, containerInput, id: link.Id);
                            resultItem.Added++;
                        }, resultItem);
                    }
                    else if (link.SourceDataPortTreeNodeId.HasValue && builder.Cache.DataPortTreeNodeGuids.TryGetValue(link.SourceDataPortTreeNodeId.Value, out var treeNode))
                    {
                        TryAndLog(() =>
                        {
                            builder.Editors.DataPortTreeNode.AssignConnector(treeNode, cacheConnector, id: link.Id);
                            resultItem.Added++;
                        }, resultItem);
                        continue;
                    }
                }
                else if (cacheConnector is IConnectorOutput && cacheContainerConnector is ContainerConnectorOutput)
                {
                    if (link.DestinationDataPortTreeNode is not null && builder.Cache.DataPortTreeNodeGuids.TryGetValue(link.DestinationDataPortTreeNode.Id, out var treeNode))
                    {
                        TryAndLog(() =>
                        {
                            builder.Editors.DataPortTreeNode.AssignConnector(treeNode, cacheConnector, id: link.Id);
                            resultItem.Added++;
                        }, resultItem);
                    }
                }
            }
        }
    }

    private static void MigrateConnectorLinks(this ClusterBuilder builder, Connector other, ClusterImportResultItem resultItem)
    {
        // we need to use the builder tree node to assign the connector
        if (!builder.Cache.ConnectorGuids.TryGetValue(other.Id, out var cacheConnector))
            return;

        foreach (var link in other.Links)
        {
            if (builder.Cache.LinkGuids.ContainsKey(link.Id))
            {
                resultItem.Skipped++;
                continue;
            }

            if (cacheConnector is IConnectorInput inputConnector)
            {
                if (link.SourceConnectorId.HasValue && builder.Cache.ConnectorGuids.TryGetValue(link.SourceConnectorId.Value, out var srcConnector))
                {
                    TryAndLog(() =>
                    {
                        builder.Editors.Connector.AddLink((IConnectorOutput)srcConnector, inputConnector, id: link.Id);
                        resultItem.Added++;
                    }, resultItem);
                }
                else if (link.SourceDataPortTreeNodeId.HasValue && builder.Cache.DataPortTreeNodeGuids.TryGetValue(link.SourceDataPortTreeNodeId.Value, out var treeNode))
                {
                    TryAndLog(() =>
                    {
                        builder.Editors.DataPortTreeNode.AssignConnector(treeNode, cacheConnector, id: link.Id);
                        resultItem.Added++;
                    }, resultItem);
                }
            }
            else if (cacheConnector is IConnectorOutput)
            {
                if (link.DestinationDataPortTreeNode is not null && builder.Cache.DataPortTreeNodeGuids.TryGetValue(link.DestinationDataPortTreeNode.Id, out var treeNode))
                {
                    TryAndLog(() =>
                    {
                        builder.Editors.DataPortTreeNode.AssignConnector(treeNode, cacheConnector, id: link.Id);
                        resultItem.Added++;
                    }, resultItem);
                }
            }
        }
    }

    private static void MigrateDataPortTreeNodeLink(this ClusterBuilder builder, DataPortTreeNode other, ClusterImportResultItem resultItem)
    {
        // we need to use the builder tree node to assign the connector
        if (!builder.Cache.DataPortTreeNodeGuids.TryGetValue(other.Id, out var dataPortTreeNode))
            return;

        foreach (var link in other.Links)
        {
            if (builder.Cache.LinkGuids.ContainsKey(link.Id))
            {
                resultItem.Skipped++;
                continue;
            }

            if (link.SourceConnectorId.HasValue && builder.Cache.ConnectorGuids.TryGetValue(link.SourceConnectorId.Value, out var connector))
            {
                TryAndLog(() =>
                {
                    builder.Editors.DataPortTreeNode.AssignConnector(dataPortTreeNode, connector, link.Id);
                    resultItem.Added++;
                }, resultItem);
            }
        }
    }

    private static void TryAndLog(Action call, ClusterImportResultItem resultItem)
    {
        try
        {
            call();
        }
        catch (Exception ex)
        {
            resultItem.Errors.Add($"{call.Method.Name}:{ex.Message}");
        }
    }
}

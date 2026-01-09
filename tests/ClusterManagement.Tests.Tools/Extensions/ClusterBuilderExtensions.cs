using ClusterManagement.Public;
using AwesomeAssertions;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Tests.Tools.Extensions;

public static class ClusterBuilderExtensions
{
    public static ClusterApplication CreateTopologyToApplication(this ClusterBuilder builder, string? name = default, Guid? id = default)
    {
        var nodeGroup = builder.Editors.Cluster.AddNodeGroup();
        var node = builder.Editors.NodeGroup.AddNode(nodeGroup);
        return builder.Editors.Node.AddApplication(node, ClusterApplicationType.CoreOsStandalone, name, id);
    }

    public static EngineHost CreateTopologyToEngineHost(this ClusterBuilder builder, string? name = default, Guid? id = default)
    {
        var nodeGroup = builder.Editors.Cluster.AddNodeGroup();
        var node = builder.Editors.NodeGroup.AddNode(nodeGroup);
        var app = builder.Editors.Node.AddApplication(node, ClusterApplicationType.CoreOsStandalone);
        return builder.Editors.Application.AddEngineHost(app, name, id);
    }

    public static Engine CreateTopologyToEngine(this ClusterBuilder builder, string name = "Engine1", Guid? id = null)
    {
        var engineHost = builder.CreateTopologyToEngineHost();
        return builder.Editors.EngineHost.AddEngine(engineHost, name, id);
    }

    public static ClusterBuilder Clone(this ClusterBuilder clusterBuilder, Version? newVersion = null)
    {
        var clusterJson = ClusterSerializer.Serialize(clusterBuilder.Cluster);
        var clusterConvert = ClusterSerializer.Deserialize(clusterJson);

        if (newVersion is not null)
            clusterConvert.Version = newVersion;

        return new(clusterConvert, clusterBuilder.DependencyResolver);
    }

    public static void ClusterShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cluster.Should()
            .BeEquivalentTo(compareTo.Cluster, opt =>
            {
                opt.Excluding(k => k.Dataflows);
                opt.Excluding(k => k.DataflowTreeNodes);
                opt.Excluding(k => k.Dependencies);
                opt.Excluding(k => k.NodeGroups);
                opt.Excluding(k => k.FunctionBlockDesignIds);

                return opt;
            });

    public static void CacheShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
    {
        builder.Cache.ClusterDependencies.Should()
            .BeEquivalentTo(compareTo.Cache.ClusterDependencies);

        builder.NodeGroupsShouldBeEquivalent(compareTo);
        builder.NodesShouldBeEquivalent(compareTo);
        builder.ApplicationsShouldBeEquivalent(compareTo);
        builder.EngineHostsShouldBeEquivalent(compareTo);
        builder.EnginesShouldBeEquivalent(compareTo);
        builder.DataPortsShouldBeEquivalent(compareTo);
        builder.DataPortTreeNodesShouldBeEquivalent(compareTo);
        builder.DataPortPropertiesShouldBeEquivalent(compareTo);
        builder.ConnectorsShouldBeEquivalent(compareTo);
        builder.ContainersShouldBeEquivalent(compareTo);
        builder.ContainerConnectorsShouldBeEquivalent(compareTo);
        builder.FunctionBlocksShouldBeEquivalent(compareTo);
        builder.LabelsShouldBeEquivalent(compareTo);
        builder.LinksShouldBeEquivalent(compareTo);
        builder.PersistentVariablesShouldBeEquivalent(compareTo);
        builder.SettingsShouldBeEquivalent(compareTo);
        builder.ApplicationPropertiesShouldBeEquivalent(compareTo);
    }

    private static void NodeGroupsShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.ClusterNodeGroups.Should()
            .BeEquivalentTo(compareTo.Cache.ClusterNodeGroups,
                options =>
                    options
                        .Excluding(c => c.Nodes));

    private static void NodesShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.ClusterNodes.Should()
            .BeEquivalentTo(compareTo.Cache.ClusterNodes,
                options =>
                    options
                        .Excluding(c => c.Applications));

    private static void ApplicationsShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.ClusterApplications.Should()
            .BeEquivalentTo(compareTo.Cache.ClusterApplications,
                options =>
                    options
                        .Excluding(c => c.EngineHosts)
                        .Excluding(c => c.Properties));

    private static void ApplicationPropertiesShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.ClusterApplicationProperties.Should()
            .BeEquivalentTo(compareTo.Cache.ClusterApplicationProperties,
                options => options.Excluding(c => c.Value));

    private static void EngineHostsShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.EngineHosts.Should()
            .BeEquivalentTo(compareTo.Cache.EngineHosts,
                options => options.Excluding(c => c.Engines));

    private static void EnginesShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.EngineGuids.Values.Should()
            .BeEquivalentTo(compareTo.Cache.EngineGuids.Values);

    private static void DataPortsShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.DataPorts.Should()
            .BeEquivalentTo(compareTo.Cache.DataPorts,
                options => options
                    .Excluding(c => c.Dataflow)
                    .Excluding(c => c.Engine)
                    .Excluding(c => c.Path.Contains(nameof(IHasDataPortTreeNodes.Nodes)))
                    .Excluding(c => c.TreeNodes));

    private static void DataPortTreeNodesShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.DataPortTreeNodes.Should()
            .BeEquivalentTo(compareTo.Cache.DataPortTreeNodes,
                options => options
                    .Excluding(c => c.Children)
                    .Excluding(c => c.Links)
                    .Excluding(c => c.IncomingLinks)
                    .Excluding(c => c.Path.Contains(nameof(IHasDataPortTreeNodes.Nodes)))
                    .Excluding(c => c.OutgoingLinks)
                    .Excluding(c => c.TransferPooling)
                    .Excluding(c => c.ValueType));

    private static void DataPortPropertiesShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.DataPortProperties.Should()
            .BeEquivalentTo(compareTo.Cache.DataPortProperties, options => options.Excluding(c => c.Value));

    private static void ConnectorsShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
    {
        builder.Cache.Connectors.Should()
            .BeEquivalentTo(compareTo.Cache.Connectors,
                options => options
                    .Excluding(c => c.Path.Contains(nameof(IConnector.Container)))
                    .Excluding(c => c.FunctionBlock)
                    .Excluding(c => c.Links)
                    .Excluding(c => c.Path.Contains(nameof(IConnector.Parent)))
                    .Excluding(c => c.Validators)
                    .Excluding(c => c.Value));

        builder.Cache.EventEnabledConnectors.Should()
            .BeEquivalentTo(compareTo.Cache.EventEnabledConnectors,
                options => options
                    .Excluding(c => c.Path.Contains(nameof(IConnector.Container)))
                    .Excluding(c => c.FunctionBlock)
                    .Excluding(c => c.Links)
                    .Excluding(c => c.Path.Contains(nameof(IConnector.Parent)))
                    .Excluding(c => c.Validators)
                    .Excluding(c => c.Value));

        builder.Cache.PublishedConnectors.Should()
            .BeEquivalentTo(compareTo.Cache.PublishedConnectors,
                options => options
                    .Excluding(c => c.Path.Contains(nameof(IConnector.Container)))
                    .Excluding(c => c.FunctionBlock)
                    .Excluding(c => c.Links)
                    .Excluding(c => c.Path.Contains(nameof(IConnector.Parent)))
                    .Excluding(c => c.Validators)
                    .Excluding(c => c.Value));
    }

    private static void ContainersShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.Containers.Should()
            .BeEquivalentTo(compareTo.Cache.Containers,
                options => options
                    .Excluding(c => c.Containers)
                    .Excluding(c => c.FunctionBlocks)
                    .Excluding(c => c.Labels));

    private static void ContainerConnectorsShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.ContainerConnectors.Should()
            .BeEquivalentTo(compareTo.Cache.ContainerConnectors,
                options => options
                    .Excluding(c => c.Container)
                    .Excluding(c => c.Path.Contains(nameof(IConnector.Container)))
                    .Excluding(c => c.Connector)
                    .Excluding(c => c.EventEnabled)
                    .Excluding(c => c.FunctionBlock)
                    .Excluding(c => c.MarkAsChangedOnlyIfNotEqual)
                    .Excluding(c => c.Links)
                    .Excluding(c => c.Path.Contains(nameof(IConnector.Parent)))
                    .Excluding(c => c.Published)
                    .Excluding(c => c.Type)
                    .Excluding(c => c.Validators)
                    .Excluding(c => c.Value));

    private static void FunctionBlocksShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.FunctionBlocks.Should()
            .BeEquivalentTo(compareTo.Cache.FunctionBlocks,
                options => options
                    .Excluding(c => c.Container)
                    .Excluding(c => c.Engine)
                    .Excluding(c => c.Inputs)
                    .Excluding(c => c.Path.Contains(nameof(IConnectable.Inputs)))
                    .Excluding(c => c.Outputs)
                    .Excluding(c => c.Path.Contains(nameof(IConnectable.Outputs)))
                    .Excluding(c => c.PersistentVariables)
                    .Excluding(c => c.Path.Contains(nameof(IHasPersistentVariables.PersistentVariables)))
                    .Excluding(c => c.Path.Contains(nameof(IContainerChild.Parent)))
                    .Excluding(c => c.ProcessDataInputs)
                    .Excluding(c => c.ProcessDataOutputs)
                    .Excluding(c => c.Settings)
                    .Excluding(c => c.Path.Contains(nameof(IHasSettings.Settings)))
                    .Excluding(c => c.SettingsInputs)
                    .Excluding(c => c.SettingsOutputs)
                    .Excluding(c => c.SystemInputs)
                    .Excluding(c => c.SystemOutputs));

    private static void LabelsShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.Labels.Should()
            .BeEquivalentTo(compareTo.Cache.Labels,
                options => options
                    .Excluding(c => c.Container)
                    .Excluding(c => c.Path.Contains(nameof(IContainerChild.Parent))));

    private static void LinksShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.Links.Should()
            .BeEquivalentTo(compareTo.Cache.Links,
                options => options
                    .Excluding(c => c.DestinationConnector)
                    .Excluding(c => c.DestinationDataPortTreeNode)
                    .Excluding(c => c.SourceConnector)
                    .Excluding(c => c.SourceDataPortTreeNode)
                    .Excluding(c => c.TypeConversion));

    private static void PersistentVariablesShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.PersistentVariables.Should()
            .BeEquivalentTo(compareTo.Cache.PersistentVariables,
                options => options
                    .Excluding(c => c.DefaultValue)
                    .Excluding(c => c.FunctionBlock)
                    .Excluding(c => c.Path.Contains(nameof(IPersistentVariable.Parent)))
                    .Excluding(c => c.Value));

    private static void SettingsShouldBeEquivalent(this ClusterBuilder builder, ClusterBuilder compareTo)
        => builder.Cache.Settings.Should()
            .BeEquivalentTo(compareTo.Cache.Settings,
                options => options
                    .Excluding(c => c.DefaultValue)
                    .Excluding(c => c.FunctionBlock)
                    .Excluding(c => c.Input)
                    .Excluding(c => c.Output)
                    .Excluding(c => c.Path.Contains(nameof(ISetting.Parent)))
                    .Excluding(c => c.Value));
}

using ClusterManagement.Public.Designs;
using ClusterManagement.Tests.Tools.Extensions;
using ClusterManagement.Tests.Tools.Factories;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;
using ViciOne.Cluster.Model.Extensions;

namespace ClusterManagement.Backend.Tests;

public partial class ComplexClusterTests
{
    [Fact]
    public void Build_complex_cluster_is_successful()
    {
        // Arrange +  Act
        using var clusterBuilder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);
        using var clusterBuilderCopy = new ClusterBuilder(clusterBuilder.Cluster, clusterBuilder.DependencyResolver);
        var clusterBuilderClone = clusterBuilder.Clone();

        // Asserts
        clusterBuilder.CacheShouldBeEquivalent(clusterBuilderClone);
        clusterBuilderCopy.CacheShouldBeEquivalent(clusterBuilderClone);
        clusterBuilder.ClusterShouldBeEquivalent(clusterBuilderClone);
    }

    [Fact]
    public void Add_functionblock_in_complex_cluster()
    {
        // Arrange +  Act
        using var clusterBuilder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);
        var resolver = (ClusterDependencyResolver)clusterBuilder.DependencyResolver;

        // add functionblock
        var childContainer1 = clusterBuilder.Cache.Containers.First(c => c.Name.Equals("ChildContainer1", StringComparison.Ordinal));
        var dep = new ClusterDependency
        {
            Name = "TestDependency",
            Version = new Version(0, 0, 1),
        };
        var designs = resolver.ResolveDependency(dep);
        var functionBlock3CC1 = clusterBuilder.Editors.Container.AddFunctionBlock(childContainer1,
            designs.First().Id,
            ClusterFactory.FunctionBlocks.FunctionBlock3CC1,
            ClusterFactory.FunctionBlocks.FunctionBlock3CC1Id
        );

        // Create ContainerConnectors for ChildContainer1
        var connectorsCC1 = new Connector[1];
        connectorsCC1[0] = functionBlock3CC1.ProcessDataInputs.First();
        clusterBuilder.Editors.Connector.AddToParentContainer(connectorsCC1);

        using var clusterBuilderCopy = new ClusterBuilder(clusterBuilder.Cluster, resolver);
        var clusterBuilderClone = clusterBuilder.Clone();

        // Asserts
        clusterBuilder.CacheShouldBeEquivalent(clusterBuilderClone);
        clusterBuilderCopy.CacheShouldBeEquivalent(clusterBuilderClone);
    }

    [Fact]
    public void Add_container_connectors_in_complex_cluster()
    {
        // Arrange
        using var clusterBuilder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);

        // Act delete connectors
        var blocks = clusterBuilder.Cache.FunctionBlocks;
        var functionBlock2CC1 = blocks.First(bl => bl.Id == ClusterFactory.FunctionBlocks.FunctionBlock2CC1Id);

        var connectorsCC1 = new Connector[1];
        connectorsCC1[0] = functionBlock2CC1.ProcessDataInputs.First();
        clusterBuilder.Editors.Connector.AddToParentContainer(connectorsCC1);

        using var clusterBuilderCopy = new ClusterBuilder(clusterBuilder.Cluster, clusterBuilder.DependencyResolver);
        var clusterBuilderClone = clusterBuilder.Clone();

        // Asserts
        clusterBuilder.CacheShouldBeEquivalent(clusterBuilderClone);
        clusterBuilderCopy.CacheShouldBeEquivalent(clusterBuilderClone);
    }

    [Fact]
    public void Delete_publish_connector_in_complex_cluster()
    {
        // Arrange
        using var clusterBuilder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);

        // Act delete publish-Connector
        var blocks = clusterBuilder.Cache.FunctionBlocks;
        var fb1 = blocks.First(bl => bl.Id == ClusterFactory.FunctionBlocks.FunctionBlock1CC1Id);
        var publishFB1Connector = fb1.ProcessDataInputs.First(bl => bl.Published);
        clusterBuilder.Editors.Connector.ResetPublished(publishFB1Connector);

        using var clusterBuilderCopy = new ClusterBuilder(clusterBuilder.Cluster, clusterBuilder.DependencyResolver);
        var clusterBuilderClone = clusterBuilder.Clone();

        // Asserts
        clusterBuilder.CacheShouldBeEquivalent(clusterBuilderClone);
        clusterBuilderCopy.CacheShouldBeEquivalent(clusterBuilderClone);
    }

    [Fact]
    public void Delete_publish_connector_in_complex_cluster_throw_exception()
    {
        // Arrange
        using var clusterBuilder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);
        var clusterBuilderClone = clusterBuilder.Clone();

        // Act delete publish-Connector
        var blocks = clusterBuilder.Cache.FunctionBlocks;
        var fb1 = blocks.First(bl => bl.Id == ClusterFactory.FunctionBlocks.FunctionBlock1CC1Id);
        var publishFB1Connector = fb1.ProcessDataInputs.First(bl => bl.Published);
        clusterBuilder.Editors.Connector.ResetPublished(publishFB1Connector);

        // Assert publish-Connector is different
        Assert.Throws<NUnit.Framework.AssertionException>(() =>
            clusterBuilder.CacheShouldBeEquivalent(clusterBuilderClone));
    }

    [Fact]
    public void Delete_functionblock_in_complex_cluster()
    {
        // Arrange
        using var clusterBuilder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);

        // Act delete functionblock
        var blocks = clusterBuilder.Cache.FunctionBlocks;
        var fb1 = blocks.First(bl => bl.Id == ClusterFactory.FunctionBlocks.FunctionBlock1CC1Id);
        clusterBuilder.Editors.Container.RemoveFunctionBlock(fb1);

        using var clusterBuilderCopy = new ClusterBuilder(clusterBuilder.Cluster, clusterBuilder.DependencyResolver);
        var clusterBuilderClone = clusterBuilder.Clone();

        // Asserts
        clusterBuilder.CacheShouldBeEquivalent(clusterBuilderClone);
        clusterBuilderCopy.CacheShouldBeEquivalent(clusterBuilderClone);
    }

    [Fact]
    public void Delete_functionblock_in_complex_cluster_throw_exception()
    {
        // Arrange
        using var clusterBuilder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);
        var clusterBuilderClone = clusterBuilder.Clone();

        // Act delete functionblock
        var blocks = clusterBuilder.Cache.FunctionBlocks;
        var fb1 = blocks.First(bl => bl.Id == ClusterFactory.FunctionBlocks.FunctionBlock1CC1Id);
        clusterBuilder.Editors.Container.RemoveFunctionBlock(fb1);

        // Asserts functionblocks are different
        Assert.Throws<NUnit.Framework.AssertionException>(() =>
            clusterBuilder.CacheShouldBeEquivalent(clusterBuilderClone));
    }

    [Fact]
    public void Delete_container_in_complex_cluster()
    {
        // Arrange
        using var clusterBuilder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);

        // Act delete container
        var containers = clusterBuilder.Cache.Containers;
        var cc2 = containers.First(c => c.Id == ClusterFactory.Containers.ChildContainer2Id);
        var cc3 = cc2.Containers.First(cc => cc.Id == ClusterFactory.Containers.ChildContainer3Id);
        clusterBuilder.Editors.Container.RemoveContainer(cc3);

        using var clusterBuilderCopy = new ClusterBuilder(clusterBuilder.Cluster, clusterBuilder.DependencyResolver);
        var clusterBuilderClone = clusterBuilder.Clone();

        // Assert
        clusterBuilder.CacheShouldBeEquivalent(clusterBuilderClone);
        clusterBuilderCopy.CacheShouldBeEquivalent(clusterBuilderClone);
    }

    [Fact]
    public void Delete_container_in_complex_cluster_throw_exception()
    {
        // Arrange
        using var clusterBuilder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);
        var clusterBuilderClone = clusterBuilder.Clone();

        // Act delete container
        var containers = clusterBuilder.Cache.Containers;
        var cc2 = containers.First(c => c.Id == ClusterFactory.Containers.ChildContainer2Id);
        var cc3 = cc2.Containers.First(cc => cc.Id == ClusterFactory.Containers.ChildContainer3Id);
        clusterBuilder.Editors.Container.RemoveContainer(cc3);

        // Assert containers are different
        Assert.Throws<NUnit.Framework.AssertionException>(() =>
            clusterBuilder.CacheShouldBeEquivalent(clusterBuilderClone));
    }

    [Fact]
    public void Delete_container_connectors_in_complex_cluster()
    {
        // Arrange
        using var clusterBuilder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);

        // Act delete connectors
        var blocks = clusterBuilder.Cache.FunctionBlocks;
        var fb1 = blocks.First(bl => bl.Id == ClusterFactory.FunctionBlocks.FunctionBlock1CC1Id);
        var connectors = fb1.GetConnectors().ToArray<IConnector>();
        clusterBuilder.Editors.Connector.RemoveFromParentContainer(connectors);

        using var clusterBuilderCopy = new ClusterBuilder(clusterBuilder.Cluster, clusterBuilder.DependencyResolver);
        var clusterBuilderClone = clusterBuilder.Clone();

        // Assert
        clusterBuilder.CacheShouldBeEquivalent(clusterBuilderClone);
        clusterBuilderCopy.CacheShouldBeEquivalent(clusterBuilderClone);
    }

    [Fact]
    public void Delete_container_connectors_in_complex_cluster_throw_exception()
    {
        // Arrange
        using var clusterBuilder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);
        var clusterBuilderClone = clusterBuilder.Clone();

        // Act delete connectors
        var blocks = clusterBuilder.Cache.FunctionBlocks;
        var fb1 = blocks.First(bl => bl.Id == ClusterFactory.FunctionBlocks.FunctionBlock1CC1Id);
        var connectors = fb1.GetConnectors().ToArray<IConnector>();
        clusterBuilder.Editors.Connector.RemoveFromParentContainer(connectors);

        // Assert connectors are different
        Assert.Throws<NUnit.Framework.AssertionException>(() =>
            clusterBuilder.CacheShouldBeEquivalent(clusterBuilderClone));
    }

    [Fact]
    public void Delete_link_in_complex_cluster()
    {
        // Arrange
        using var clusterBuilder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);

        // Act delete link
        var links = clusterBuilder.Cache.Links;
        var link1 = links.First(l => l.Id == ClusterFactory.Links.LinkId1);
        clusterBuilder.Editors.Connector.RemoveLink(link1);

        using var clusterBuilderCopy = new ClusterBuilder(clusterBuilder.Cluster, clusterBuilder.DependencyResolver);
        var clusterBuilderClone = clusterBuilder.Clone();

        // Assert
        clusterBuilder.CacheShouldBeEquivalent(clusterBuilderClone);
        clusterBuilderCopy.CacheShouldBeEquivalent(clusterBuilderClone);
    }

    [Fact]
    public void Delete_link_in_complex_cluster_throw_exception()
    {
        // Arrange
        using var clusterBuilder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);
        var clusterBuilderClone = clusterBuilder.Clone();

        // Act delete connectors
        var links = clusterBuilder.Cache.Links;
        var link1 = links.First(bl => bl.Id == ClusterFactory.Links.LinkId1);
        clusterBuilder.Editors.Connector.RemoveLink(link1);

        // Assert links are different
        Assert.Throws<NUnit.Framework.AssertionException>(() =>
            clusterBuilder.CacheShouldBeEquivalent(clusterBuilderClone));
    }
}

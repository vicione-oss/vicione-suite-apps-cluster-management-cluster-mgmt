using Bunit;
using ClusterManagement.Client.Management;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Client;
using Range = System.Range;

namespace ClusterManagement.Client.Tests.Management;

public sealed class ClusterManagementPageTests
{
    private readonly IClusterManagementService _managementService = Substitute.For<IClusterManagementService>();

    [Fact]
    public void PageGetsRendered()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddLocalization<ClusterManagementClientModule>();
            setup.ClientMediator.Request<GetDeployClusterStateRequest, GetDeployClusterStateResponse>(Arg.Any<GetDeployClusterStateRequest>(), Arg.Any<CancellationToken>())
                .Returns(new GetDeployClusterStateResponse()
                {
                    State = new DeploymentState(Guid.NewGuid(), "Deployed", new Version(1, 0, 0), new Version(1, 1, 0))
                });
        });
        ctx.SetupDevExpressBlazor();
        ctx.Services.AddSingleton(_managementService);
        ctx.Services.AddSingleton(Substitute.For<IClusterService>());
        ctx.Services.AddSingleton(Substitute.For<IDesignProvider>());

        _managementService.QueryClusterVersions(null, Arg.Any<CancellationToken>())
            .Returns(TestDataFactory.CreateClusterViewModels(Guid.NewGuid()));

        // Act
        var sut = ctx.Render<ClusterManagementPage>();

        // Assert
        Assert.NotNull(sut);
    }


    [Fact]
    public void Cluster_view_models_should_be_sorted_by_id_and_version()
    {
        // Arrange
        var cluster1UniqueId = new Guid("A85FAEE1-18AF-4F78-B795-238921AD1313");
        var cluster2UniqueId = new Guid("D34E5F28-96A1-4DCE-AF29-A6B51E62132E");
        var cluster3UniqueId = new Guid("BA389BEF-9C84-4BA7-A9D0-FCFB1C2FDD0C");

        var list = TestDataFactory.CreateClusterViewModels(cluster1UniqueId, cluster2UniqueId, cluster3UniqueId);
        var clusterCount1 = list.Count(k => k.Id == cluster1UniqueId);
        var clusterCount2 = list.Count(k => k.Id == cluster2UniqueId);
        var clusterCount3 = list.Count(k => k.Id == cluster3UniqueId);

        // Act
        list.Sort();

        var cluster1Versions = list.Take(new Range(0, clusterCount1)).ToList();
        var cluster3Versions = list.Take(new Range(clusterCount1, clusterCount1 + clusterCount3));
        var cluster2Versions = list.Take(new Range(clusterCount1 + clusterCount3, clusterCount1 + clusterCount3 + clusterCount2));

        // Assert
        cluster1Versions.Should().AllSatisfy(c => c.Id.Should().Be(cluster1UniqueId));
        cluster2Versions.Should().AllSatisfy(c => c.Id.Should().Be(cluster2UniqueId));
        cluster3Versions.Should().AllSatisfy(c => c.Id.Should().Be(cluster3UniqueId));
        cluster1Versions.Should().BeInDescendingOrder(k => k.Version);
    }
}

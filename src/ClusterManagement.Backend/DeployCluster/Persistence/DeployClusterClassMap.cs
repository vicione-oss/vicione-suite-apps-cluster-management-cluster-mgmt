using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Backend.Extensions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sdk.Backend.Extensions;

namespace ClusterManagement.Backend.DeployCluster.Persistence;

public sealed class DeployClusterClassMap : SagaClassMap<DeployClusterState>
{
    protected override void Configure(EntityTypeBuilder<DeployClusterState> entity, ModelBuilder model)
    {
        entity.Property(x => x.ClusterApplicationsToUpdate).PersistAsJson();
        entity.Property(x => x.ClusterApplicationsUpdateFinished).PersistAsJson();

        entity.Property(x => x.ClusterNodesToCreate).PersistAsJson();
        entity.Property(x => x.ClusterNodesCreated).PersistAsJson();
        entity.Property(x => x.ClusterApplicationStarted).PersistAsJson();

        entity.Property(x => x.ClusterNodesToClean).PersistAsJson();
        entity.Property(x => x.ClusterNodesCleaned).PersistAsJson();

        entity.Property(x => x.ActiveVersion).PersistAsString();
        entity.Property(x => x.TargetVersion).PersistAsString();

        // If using Optimistic concurrency, otherwise remove this property
        entity.Property(x => x.RowVersion).IsRowVersion();
    }
}

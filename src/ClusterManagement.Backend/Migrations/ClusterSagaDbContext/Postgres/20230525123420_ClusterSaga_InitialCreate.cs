using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClusterManagement.Backend.Ef.Migrations.ClusterSagaDbContext.Postgres
{
    /// <inheritdoc />
    public partial class ClusterSaga_InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "clustersaga");

            migrationBuilder.CreateTable(
                name: "DeployClusterState",
                schema: "clustersaga",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CurrentState = table.Column<int>(type: "integer", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActiveVersion = table.Column<string>(type: "text", nullable: false),
                    TargetVersion = table.Column<string>(type: "text", nullable: false),
                    ClusterApplicationsToUpdate = table.Column<string>(type: "text", nullable: false),
                    ClusterApplicationsUpdateFinished = table.Column<string>(type: "text", nullable: false),
                    ClusterNodesToCreate = table.Column<string>(type: "text", nullable: false),
                    ClusterNodesCreated = table.Column<string>(type: "text", nullable: false),
                    ClusterApplicationStarted = table.Column<string>(type: "text", nullable: false),
                    ClusterNodesToClean = table.Column<string>(type: "text", nullable: false),
                    ClusterNodesCleaned = table.Column<string>(type: "text", nullable: false),
                    AtLeastOneDeployReturnedError = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeployClusterState", x => x.CorrelationId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeployClusterState",
                schema: "clustersaga");
        }
    }
}

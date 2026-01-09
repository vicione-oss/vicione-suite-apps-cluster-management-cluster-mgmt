using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClusterManagement.Backend.Ef.Migrations.ClusterSagaDbContext.Sqlite
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
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    CurrentState = table.Column<int>(type: "INTEGER", nullable: false),
                    ClusterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActiveVersion = table.Column<string>(type: "TEXT", nullable: false),
                    TargetVersion = table.Column<string>(type: "TEXT", nullable: false),
                    ClusterApplicationsToUpdate = table.Column<string>(type: "TEXT", nullable: false),
                    ClusterApplicationsUpdateFinished = table.Column<string>(type: "TEXT", nullable: false),
                    ClusterNodesToCreate = table.Column<string>(type: "TEXT", nullable: false),
                    ClusterNodesCreated = table.Column<string>(type: "TEXT", nullable: false),
                    ClusterApplicationStarted = table.Column<string>(type: "TEXT", nullable: false),
                    ClusterNodesToClean = table.Column<string>(type: "TEXT", nullable: false),
                    ClusterNodesCleaned = table.Column<string>(type: "TEXT", nullable: false),
                    AtLeastOneDeployReturnedError = table.Column<bool>(type: "INTEGER", nullable: false)
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

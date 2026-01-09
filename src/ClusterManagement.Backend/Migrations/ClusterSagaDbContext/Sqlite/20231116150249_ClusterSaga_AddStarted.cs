using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClusterManagement.Backend.Migrations.ClusterSagaDbContext.Sqlite
{
    /// <inheritdoc />
    public partial class ClusterSaga_AddStarted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Started",
                schema: "clustersaga",
                table: "DeployClusterState",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Started",
                schema: "clustersaga",
                table: "DeployClusterState");
        }
    }
}

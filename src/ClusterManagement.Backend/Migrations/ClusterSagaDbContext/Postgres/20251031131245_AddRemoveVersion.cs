using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClusterManagement.Backend.Migrations.ClusterSagaDbContext.Postgres
{
    /// <inheritdoc />
    public partial class AddRemoveVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RemoveVersion",
                schema: "clustersaga",
                table: "DeployClusterState",
                type: "text",
                nullable: false,
                defaultValue: "0.0.0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemoveVersion",
                schema: "clustersaga",
                table: "DeployClusterState");
        }
    }
}

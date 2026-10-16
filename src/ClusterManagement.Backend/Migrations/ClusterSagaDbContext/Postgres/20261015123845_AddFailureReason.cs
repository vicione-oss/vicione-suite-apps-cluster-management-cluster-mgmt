using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClusterManagement.Backend.Migrations.ClusterSagaDbContext.Postgres
{
    /// <inheritdoc />
    public partial class AddFailureReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitializeFailed",
                schema: "clustersaga",
                table: "DeployClusterState");

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                schema: "clustersaga",
                table: "DeployClusterState",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureReason",
                schema: "clustersaga",
                table: "DeployClusterState");

            migrationBuilder.AddColumn<bool>(
                name: "InitializeFailed",
                schema: "clustersaga",
                table: "DeployClusterState",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}

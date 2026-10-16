using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClusterManagement.Backend.Migrations.ClusterSagaDbContext.Sqlite
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
                type: "TEXT",
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
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}

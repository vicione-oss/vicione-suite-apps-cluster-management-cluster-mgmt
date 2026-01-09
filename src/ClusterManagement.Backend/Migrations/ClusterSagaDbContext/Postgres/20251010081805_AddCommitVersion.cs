using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClusterManagement.Backend.Migrations.ClusterSagaDbContext.Postgres
{
    /// <inheritdoc />
    public partial class AddCommitVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommitVersion",
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
                name: "CommitVersion",
                schema: "clustersaga",
                table: "DeployClusterState");
        }
    }
}

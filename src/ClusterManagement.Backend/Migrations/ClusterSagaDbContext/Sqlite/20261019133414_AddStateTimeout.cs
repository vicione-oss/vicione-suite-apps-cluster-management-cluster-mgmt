using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClusterManagement.Backend.Migrations.ClusterSagaDbContext.Sqlite
{
    /// <inheritdoc />
    public partial class AddStateTimeout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StateTimeoutAt",
                schema: "clustersaga",
                table: "DeployClusterState",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StateTimeoutAt",
                schema: "clustersaga",
                table: "DeployClusterState");
        }
    }
}

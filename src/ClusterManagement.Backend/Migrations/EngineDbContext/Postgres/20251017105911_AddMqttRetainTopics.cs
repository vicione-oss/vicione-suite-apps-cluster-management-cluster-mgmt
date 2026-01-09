using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClusterManagement.Backend.Migrations.EngineDbContext.Postgres
{
    /// <inheritdoc />
    public partial class AddMqttRetainTopics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<Guid>>(
                name: "MqttRetainTopics",
                schema: "engines",
                table: "EngineDeployments",
                type: "uuid[]",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MqttRetainTopics",
                schema: "engines",
                table: "EngineDeployments");
        }
    }
}

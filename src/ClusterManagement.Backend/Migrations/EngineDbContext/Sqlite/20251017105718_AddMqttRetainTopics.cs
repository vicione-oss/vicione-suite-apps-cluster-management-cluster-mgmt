using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClusterManagement.Backend.Migrations.EngineDbContext.Sqlite
{
    /// <inheritdoc />
    public partial class AddMqttRetainTopics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MqttRetainTopics",
                schema: "engines",
                table: "EngineDeployments",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
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

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClusterManagement.Backend.Migrations.EngineDbContext.Postgres
{
    /// <inheritdoc />
    public partial class AddEngineHostElevatedPrivileges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ElevatedPrivileges",
                schema: "engines",
                table: "EngineHosts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ElevatedPrivileges",
                schema: "engines",
                table: "EngineHosts");
        }
    }
}

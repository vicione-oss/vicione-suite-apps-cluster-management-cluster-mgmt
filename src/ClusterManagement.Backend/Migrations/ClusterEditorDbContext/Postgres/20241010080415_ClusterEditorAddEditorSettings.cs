using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClusterManagement.Backend.Migrations.ClusterEditorDbContext.Postgres
{
    /// <inheritdoc />
    public partial class ClusterEditorAddEditorSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "clustereditor");

            migrationBuilder.CreateTable(
                name: "Settings",
                schema: "clustereditor",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    GridMode = table.Column<int>(type: "integer", nullable: false),
                    MinimapNodeColoring = table.Column<bool>(type: "boolean", nullable: false),
                    NodeAlignmentBorder = table.Column<bool>(type: "boolean", nullable: false),
                    PanBehavior = table.Column<bool>(type: "boolean", nullable: false),
                    SimplifiedView = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.UserId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Settings",
                schema: "clustereditor");
        }
    }
}

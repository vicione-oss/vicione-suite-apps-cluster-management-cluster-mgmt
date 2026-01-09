using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClusterManagement.Backend.Migrations.EngineDbContext.Sqlite
{
    /// <inheritdoc />
    public partial class Engine_AddOutgoingLinkRemoteId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RemoteId",
                schema: "engines",
                table: "OutgoingLink",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE OutgoingLink SET RemoteId = ConnectorId; ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemoteId",
                schema: "engines",
                table: "OutgoingLink");
        }
    }
}

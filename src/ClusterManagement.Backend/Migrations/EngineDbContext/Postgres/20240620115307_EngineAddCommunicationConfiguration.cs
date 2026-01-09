using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClusterManagement.Backend.Migrations.EngineDbContext.Postgres
{
    /// <inheritdoc />
    public partial class EngineAddCommunicationConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TypedObject_ExternalConnection_ExternalConnectionId",
                schema: "engines",
                table: "TypedObject");

            migrationBuilder.RenameColumn(
                name: "ExternalConnectionId",
                schema: "engines",
                table: "TypedObject",
                newName: "CommunicationPropertyId");

            migrationBuilder.RenameIndex(
                name: "IX_TypedObject_ExternalConnectionId",
                schema: "engines",
                table: "TypedObject",
                newName: "IX_TypedObject_CommunicationPropertyId");

            migrationBuilder.CreateTable(
                name: "CommunicationConfiguration",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Id = table.Column<string>(type: "text", nullable: true),
                    ExternalConnectionId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationConfiguration", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_CommunicationConfiguration_ExternalConnection_ExternalConne~",
                        column: x => x.ExternalConnectionId,
                        principalSchema: "engines",
                        principalTable: "ExternalConnection",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationProperty",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Id = table.Column<string>(type: "text", nullable: true),
                    CommunicationConfigurationDatabaseId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationProperty", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_CommunicationProperty_CommunicationConfiguration_Communicat~",
                        column: x => x.CommunicationConfigurationDatabaseId,
                        principalSchema: "engines",
                        principalTable: "CommunicationConfiguration",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationConfiguration_ExternalConnectionId",
                schema: "engines",
                table: "CommunicationConfiguration",
                column: "ExternalConnectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationProperty_CommunicationConfigurationDatabaseId",
                schema: "engines",
                table: "CommunicationProperty",
                column: "CommunicationConfigurationDatabaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_TypedObject_CommunicationProperty_CommunicationPropertyId",
                schema: "engines",
                table: "TypedObject",
                column: "CommunicationPropertyId",
                principalSchema: "engines",
                principalTable: "CommunicationProperty",
                principalColumn: "DatabaseId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TypedObject_CommunicationProperty_CommunicationPropertyId",
                schema: "engines",
                table: "TypedObject");

            migrationBuilder.DropTable(
                name: "CommunicationProperty",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "CommunicationConfiguration",
                schema: "engines");

            migrationBuilder.RenameColumn(
                name: "CommunicationPropertyId",
                schema: "engines",
                table: "TypedObject",
                newName: "ExternalConnectionId");

            migrationBuilder.RenameIndex(
                name: "IX_TypedObject_CommunicationPropertyId",
                schema: "engines",
                table: "TypedObject",
                newName: "IX_TypedObject_ExternalConnectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_TypedObject_ExternalConnection_ExternalConnectionId",
                schema: "engines",
                table: "TypedObject",
                column: "ExternalConnectionId",
                principalSchema: "engines",
                principalTable: "ExternalConnection",
                principalColumn: "DatabaseId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

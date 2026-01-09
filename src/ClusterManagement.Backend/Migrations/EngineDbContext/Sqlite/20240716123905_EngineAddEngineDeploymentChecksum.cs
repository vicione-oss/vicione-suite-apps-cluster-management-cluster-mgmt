using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClusterManagement.Backend.Migrations.EngineDbContext.Sqlite
{
    /// <inheritdoc />
    public partial class EngineAddEngineDeploymentChecksum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClusterDependency",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "Container",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "Engine",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "FunctionBlockConnector",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "FunctionBlockConnectorDesign",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "FunctionBlockConnectorLink",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "FunctionBlockSetting",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "FunctionBlockVariable",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "IncomingLink",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "MqttCommunication",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "OutgoingLink",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "FunctionBlock",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "TypedObject",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "CommunicationProperty",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "CommunicationConfiguration",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "ExternalConnection",
                schema: "engines");

            migrationBuilder.DropTable(
                name: "Workflow",
                schema: "engines");

            migrationBuilder.AddColumn<int>(
                name: "Checksum",
                schema: "engines",
                table: "EngineDeployments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Checksum",
                schema: "engines",
                table: "EngineDeployments");

            migrationBuilder.CreateTable(
                name: "Workflow",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EngineDeploymentId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflow", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_Workflow_EngineDeployments_EngineDeploymentId",
                        column: x => x.EngineDeploymentId,
                        principalSchema: "engines",
                        principalTable: "EngineDeployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClusterDependency",
                schema: "engines",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    WorkflowDatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClusterDependency", x => new { x.Name, x.Version, x.WorkflowDatabaseId });
                    table.ForeignKey(
                        name: "FK_ClusterDependency_Workflow_WorkflowDatabaseId",
                        column: x => x.WorkflowDatabaseId,
                        principalSchema: "engines",
                        principalTable: "Workflow",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Container",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ParentContainerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WorkflowDatabaseId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Container", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_Container_Workflow_WorkflowDatabaseId",
                        column: x => x.WorkflowDatabaseId,
                        principalSchema: "engines",
                        principalTable: "Workflow",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Engine",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChainIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    CycleTime = table.Column<uint>(type: "INTEGER", nullable: false),
                    LogLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Engine", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_Engine_Workflow_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "engines",
                        principalTable: "Workflow",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalConnection",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkflowDatabaseId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalConnection", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_ExternalConnection_Workflow_WorkflowDatabaseId",
                        column: x => x.WorkflowDatabaseId,
                        principalSchema: "engines",
                        principalTable: "Workflow",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FunctionBlock",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContainerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CycleFrequency = table.Column<uint>(type: "INTEGER", nullable: false),
                    DesignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    RunIndex = table.Column<uint>(type: "INTEGER", nullable: false),
                    RunMode = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkflowDatabaseId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionBlock", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_FunctionBlock_Workflow_WorkflowDatabaseId",
                        column: x => x.WorkflowDatabaseId,
                        principalSchema: "engines",
                        principalTable: "Workflow",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FunctionBlockConnectorDesign",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConnectorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsInput = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ValueType = table.Column<string>(type: "TEXT", nullable: false),
                    WorkflowDatabaseId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionBlockConnectorDesign", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_FunctionBlockConnectorDesign_Workflow_WorkflowDatabaseId",
                        column: x => x.WorkflowDatabaseId,
                        principalSchema: "engines",
                        principalTable: "Workflow",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MqttCommunication",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    Password = table.Column<string>(type: "TEXT", nullable: true),
                    PersistenceCommunicationId = table.Column<int>(type: "INTEGER", nullable: true),
                    Protocol = table.Column<int>(type: "INTEGER", nullable: false),
                    RuntimeFunctionsCommunicationId = table.Column<int>(type: "INTEGER", nullable: true),
                    TcpPort = table.Column<int>(type: "INTEGER", nullable: true),
                    Username = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MqttCommunication", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_MqttCommunication_Workflow_PersistenceCommunicationId",
                        column: x => x.PersistenceCommunicationId,
                        principalSchema: "engines",
                        principalTable: "Workflow",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MqttCommunication_Workflow_RuntimeFunctionsCommunicationId",
                        column: x => x.RuntimeFunctionsCommunicationId,
                        principalSchema: "engines",
                        principalTable: "Workflow",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationConfiguration",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExternalConnectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Id = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationConfiguration", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_CommunicationConfiguration_ExternalConnection_ExternalConnectionId",
                        column: x => x.ExternalConnectionId,
                        principalSchema: "engines",
                        principalTable: "ExternalConnection",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutgoingLink",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConnectorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalConnectionDatabaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    RemoteId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutgoingLink", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_OutgoingLink_ExternalConnection_ExternalConnectionDatabaseId",
                        column: x => x.ExternalConnectionDatabaseId,
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
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommunicationConfigurationDatabaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    Id = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationProperty", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_CommunicationProperty_CommunicationConfiguration_CommunicationConfigurationDatabaseId",
                        column: x => x.CommunicationConfigurationDatabaseId,
                        principalSchema: "engines",
                        principalTable: "CommunicationConfiguration",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TypedObject",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommunicationPropertyId = table.Column<int>(type: "INTEGER", nullable: true),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true),
                    WorkflowDatabaseId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TypedObject", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_TypedObject_CommunicationProperty_CommunicationPropertyId",
                        column: x => x.CommunicationPropertyId,
                        principalSchema: "engines",
                        principalTable: "CommunicationProperty",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TypedObject_Workflow_WorkflowDatabaseId",
                        column: x => x.WorkflowDatabaseId,
                        principalSchema: "engines",
                        principalTable: "Workflow",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FunctionBlockConnector",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AggregatingPoolingDatabaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    InitialValueDatabaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    ValidatorDatabaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    DesignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FunctionBlockDatabaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarkAsChangedOnlyIfNotEqual = table.Column<bool>(type: "INTEGER", nullable: false),
                    PoolingStrategy = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionBlockConnector", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_FunctionBlockConnector_FunctionBlock_FunctionBlockDatabaseId",
                        column: x => x.FunctionBlockDatabaseId,
                        principalSchema: "engines",
                        principalTable: "FunctionBlock",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FunctionBlockConnector_TypedObject_AggregatingPoolingDatabaseId",
                        column: x => x.AggregatingPoolingDatabaseId,
                        principalSchema: "engines",
                        principalTable: "TypedObject",
                        principalColumn: "DatabaseId");
                    table.ForeignKey(
                        name: "FK_FunctionBlockConnector_TypedObject_InitialValueDatabaseId",
                        column: x => x.InitialValueDatabaseId,
                        principalSchema: "engines",
                        principalTable: "TypedObject",
                        principalColumn: "DatabaseId");
                    table.ForeignKey(
                        name: "FK_FunctionBlockConnector_TypedObject_ValidatorDatabaseId",
                        column: x => x.ValidatorDatabaseId,
                        principalSchema: "engines",
                        principalTable: "TypedObject",
                        principalColumn: "DatabaseId");
                });

            migrationBuilder.CreateTable(
                name: "FunctionBlockConnectorLink",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TypeConverterDatabaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    DestinationConnectorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceConnectorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDatabaseId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionBlockConnectorLink", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_FunctionBlockConnectorLink_TypedObject_TypeConverterDatabaseId",
                        column: x => x.TypeConverterDatabaseId,
                        principalSchema: "engines",
                        principalTable: "TypedObject",
                        principalColumn: "DatabaseId");
                    table.ForeignKey(
                        name: "FK_FunctionBlockConnectorLink_Workflow_WorkflowDatabaseId",
                        column: x => x.WorkflowDatabaseId,
                        principalSchema: "engines",
                        principalTable: "Workflow",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FunctionBlockSetting",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ValueDatabaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    DesignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FunctionBlockDatabaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionBlockSetting", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_FunctionBlockSetting_FunctionBlock_FunctionBlockDatabaseId",
                        column: x => x.FunctionBlockDatabaseId,
                        principalSchema: "engines",
                        principalTable: "FunctionBlock",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FunctionBlockSetting_TypedObject_ValueDatabaseId",
                        column: x => x.ValueDatabaseId,
                        principalSchema: "engines",
                        principalTable: "TypedObject",
                        principalColumn: "DatabaseId");
                });

            migrationBuilder.CreateTable(
                name: "FunctionBlockVariable",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ValueDatabaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    DesignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FunctionBlockDatabaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionBlockVariable", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_FunctionBlockVariable_FunctionBlock_FunctionBlockDatabaseId",
                        column: x => x.FunctionBlockDatabaseId,
                        principalSchema: "engines",
                        principalTable: "FunctionBlock",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FunctionBlockVariable_TypedObject_ValueDatabaseId",
                        column: x => x.ValueDatabaseId,
                        principalSchema: "engines",
                        principalTable: "TypedObject",
                        principalColumn: "DatabaseId");
                });

            migrationBuilder.CreateTable(
                name: "IncomingLink",
                schema: "engines",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TypeConverterDatabaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    ConnectorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalConnectionDatabaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    RemoteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomingLink", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_IncomingLink_ExternalConnection_ExternalConnectionDatabaseId",
                        column: x => x.ExternalConnectionDatabaseId,
                        principalSchema: "engines",
                        principalTable: "ExternalConnection",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncomingLink_TypedObject_TypeConverterDatabaseId",
                        column: x => x.TypeConverterDatabaseId,
                        principalSchema: "engines",
                        principalTable: "TypedObject",
                        principalColumn: "DatabaseId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClusterDependency_WorkflowDatabaseId",
                schema: "engines",
                table: "ClusterDependency",
                column: "WorkflowDatabaseId");

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

            migrationBuilder.CreateIndex(
                name: "IX_Container_WorkflowDatabaseId",
                schema: "engines",
                table: "Container",
                column: "WorkflowDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Engine_WorkflowId",
                schema: "engines",
                table: "Engine",
                column: "WorkflowId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalConnection_WorkflowDatabaseId",
                schema: "engines",
                table: "ExternalConnection",
                column: "WorkflowDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionBlock_WorkflowDatabaseId",
                schema: "engines",
                table: "FunctionBlock",
                column: "WorkflowDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionBlockConnector_AggregatingPoolingDatabaseId",
                schema: "engines",
                table: "FunctionBlockConnector",
                column: "AggregatingPoolingDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionBlockConnector_FunctionBlockDatabaseId",
                schema: "engines",
                table: "FunctionBlockConnector",
                column: "FunctionBlockDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionBlockConnector_InitialValueDatabaseId",
                schema: "engines",
                table: "FunctionBlockConnector",
                column: "InitialValueDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionBlockConnector_ValidatorDatabaseId",
                schema: "engines",
                table: "FunctionBlockConnector",
                column: "ValidatorDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionBlockConnectorDesign_WorkflowDatabaseId",
                schema: "engines",
                table: "FunctionBlockConnectorDesign",
                column: "WorkflowDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionBlockConnectorLink_TypeConverterDatabaseId",
                schema: "engines",
                table: "FunctionBlockConnectorLink",
                column: "TypeConverterDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionBlockConnectorLink_WorkflowDatabaseId",
                schema: "engines",
                table: "FunctionBlockConnectorLink",
                column: "WorkflowDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionBlockSetting_FunctionBlockDatabaseId",
                schema: "engines",
                table: "FunctionBlockSetting",
                column: "FunctionBlockDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionBlockSetting_ValueDatabaseId",
                schema: "engines",
                table: "FunctionBlockSetting",
                column: "ValueDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionBlockVariable_FunctionBlockDatabaseId",
                schema: "engines",
                table: "FunctionBlockVariable",
                column: "FunctionBlockDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionBlockVariable_ValueDatabaseId",
                schema: "engines",
                table: "FunctionBlockVariable",
                column: "ValueDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingLink_ExternalConnectionDatabaseId",
                schema: "engines",
                table: "IncomingLink",
                column: "ExternalConnectionDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingLink_TypeConverterDatabaseId",
                schema: "engines",
                table: "IncomingLink",
                column: "TypeConverterDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_MqttCommunication_PersistenceCommunicationId",
                schema: "engines",
                table: "MqttCommunication",
                column: "PersistenceCommunicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MqttCommunication_RuntimeFunctionsCommunicationId",
                schema: "engines",
                table: "MqttCommunication",
                column: "RuntimeFunctionsCommunicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingLink_ExternalConnectionDatabaseId",
                schema: "engines",
                table: "OutgoingLink",
                column: "ExternalConnectionDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TypedObject_CommunicationPropertyId",
                schema: "engines",
                table: "TypedObject",
                column: "CommunicationPropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TypedObject_WorkflowDatabaseId",
                schema: "engines",
                table: "TypedObject",
                column: "WorkflowDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Workflow_EngineDeploymentId",
                schema: "engines",
                table: "Workflow",
                column: "EngineDeploymentId",
                unique: true);
        }
    }
}

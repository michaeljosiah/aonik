using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkInsights_SubjectType_SubjectId",
                schema: "dbo",
                table: "AnkInsights");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkTransmissions",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkToolSpecs",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkRoutingRules",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkRefunds",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkPromptSpecs",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkPayoutSchemas",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkPayouts",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkPayments",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkOrderNotes",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkOrderFundingRefs",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkLimitsPolicies",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkLedgers",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkInvoiceAllocations",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                schema: "dbo",
                table: "AnkInsights",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                schema: "dbo",
                table: "AnkInsights",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "dbo",
                table: "AnkInsights",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                schema: "dbo",
                table: "AnkInsights",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkFxQuotes",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkFeePolicies",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkEvalSuites",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkEvalRuns",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkDunningPlans",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkCustomerAccounts",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkConnectors",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkChargebacks",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkBalanceSnapshots",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkAiTraces",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkAiRoutePolicies",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkAiPolicies",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkAiFeedbacks",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AddColumn<int>(
                name: "AgentType",
                schema: "dbo",
                table: "AnkAgents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ConversationSummaries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChatThreadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionStartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SessionEndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SummaryText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    KeyDecisionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OpenLoopsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecommendationOutcomesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationSummaries_AnkChatThreads_ChatThreadId",
                        column: x => x.ChatThreadId,
                        principalSchema: "dbo",
                        principalTable: "AnkChatThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMemoryEntry",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ValueJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AiRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupersededById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMemoryEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMemoryEntry_AnkAiRuns_AiRunId",
                        column: x => x.AiRunId,
                        principalSchema: "dbo",
                        principalTable: "AnkAiRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserMemoryEntry_UserMemoryEntry_SupersededById",
                        column: x => x.SupersededById,
                        principalSchema: "dbo",
                        principalTable: "UserMemoryEntry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Insights_Tenant_SubjectType_SubjectId",
                schema: "dbo",
                table: "AnkInsights",
                columns: new[] { "TenantId", "SubjectType", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Insights_Tenant_UserId",
                schema: "dbo",
                table: "AnkInsights",
                columns: new[] { "TenantId", "UserId" },
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaries_ChatThreadId",
                schema: "dbo",
                table: "ConversationSummaries",
                column: "ChatThreadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaries_TenantUser_SessionStart",
                schema: "dbo",
                table: "ConversationSummaries",
                columns: new[] { "TenantId", "UserId", "SessionStartedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_UserMemoryEntries_TenantUser_Current",
                schema: "dbo",
                table: "UserMemoryEntry",
                columns: new[] { "TenantId", "UserId", "SupersededById" },
                filter: "[SupersededById] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemoryEntries_TenantUser_EntryType",
                schema: "dbo",
                table: "UserMemoryEntry",
                columns: new[] { "TenantId", "UserId", "EntryType" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMemoryEntries_TenantUser_Key",
                schema: "dbo",
                table: "UserMemoryEntry",
                columns: new[] { "TenantId", "UserId", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMemoryEntry_AiRunId",
                schema: "dbo",
                table: "UserMemoryEntry",
                column: "AiRunId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemoryEntry_SupersededById",
                schema: "dbo",
                table: "UserMemoryEntry",
                column: "SupersededById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationSummaries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UserMemoryEntry",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Insights_Tenant_SubjectType_SubjectId",
                schema: "dbo",
                table: "AnkInsights");

            migrationBuilder.DropIndex(
                name: "IX_Insights_Tenant_UserId",
                schema: "dbo",
                table: "AnkInsights");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                schema: "dbo",
                table: "AnkInsights");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                schema: "dbo",
                table: "AnkInsights");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "dbo",
                table: "AnkInsights");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "dbo",
                table: "AnkInsights");

            migrationBuilder.DropColumn(
                name: "AgentType",
                schema: "dbo",
                table: "AnkAgents");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkTransmissions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkToolSpecs",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkRoutingRules",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkRefunds",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkPromptSpecs",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkPayoutSchemas",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkPayouts",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkPayments",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkOrderNotes",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkOrderFundingRefs",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkLimitsPolicies",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkLedgers",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkInvoiceAllocations",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkFxQuotes",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkFeePolicies",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkEvalSuites",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkEvalRuns",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkDunningPlans",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkCustomerAccounts",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkConnectors",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkChargebacks",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkBalanceSnapshots",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkAiTraces",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkAiRoutePolicies",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkAiPolicies",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "AnkAiFeedbacks",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.CreateIndex(
                name: "IX_AnkInsights_SubjectType_SubjectId",
                schema: "dbo",
                table: "AnkInsights",
                columns: new[] { "SubjectType", "SubjectId" });
        }
    }
}

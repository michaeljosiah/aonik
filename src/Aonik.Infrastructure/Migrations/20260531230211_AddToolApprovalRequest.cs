using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddToolApprovalRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkToolApprovalRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestingUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ThreadId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AiRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToolName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ToolCallId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ArgumentsRedactedJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArgsHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RiskTier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActionKind = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnkToolApprovalRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ToolApprovalRequest_ExpiresAt",
                schema: "dbo",
                table: "AnkToolApprovalRequests",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ToolApprovalRequest_Tenant_Status",
                schema: "dbo",
                table: "AnkToolApprovalRequests",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolApprovalRequest_Tenant_Tool_ArgsHash_Status",
                schema: "dbo",
                table: "AnkToolApprovalRequests",
                columns: new[] { "TenantId", "ToolName", "ArgsHash", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolApprovalRequest_ThreadId",
                schema: "dbo",
                table: "AnkToolApprovalRequests",
                column: "ThreadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkToolApprovalRequests",
                schema: "dbo");
        }
    }
}

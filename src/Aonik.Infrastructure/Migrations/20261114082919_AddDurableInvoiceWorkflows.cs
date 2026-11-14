using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableInvoiceWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActorId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorType",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckpointId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Continuation",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DefinitionHash",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FactsHash",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FactsJson",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrameworkSessionId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<Guid>(
                name: "InitiatingUserId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InputHash",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAt",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Legacy");

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingStepRunId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectType",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowVersionId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowStepRunId",
                schema: "dbo",
                table: "AnkToolApprovalRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowStepRunId",
                schema: "dbo",
                table: "AnkProposals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IssueApprovalRequestId",
                schema: "dbo",
                table: "AnkInvoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IssueApprovedByUserId",
                schema: "dbo",
                table: "AnkInvoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IssueOperationId",
                schema: "dbo",
                table: "AnkInvoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnkWorkflowCheckpoints",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FrameworkSessionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CheckpointId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ParentCheckpointId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    FormatVersion = table.Column<int>(type: "int", nullable: false),
                    FrameworkVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_AnkWorkflowCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkWorkflowStepRuns",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                    InvocationPath = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActionKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ActionVersion = table.Column<int>(type: "int", nullable: true),
                    RequiredPermission = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArgumentsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ArgumentsHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FactsHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FactsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpectedSubjectVersion = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    WaitCheckpointId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecisionTraceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SelectedRoute = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AiRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
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
                    table.PrimaryKey("PK_AnkWorkflowStepRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkWorkflowRuns_TenantId_ActorType_ActorId_IdempotencyKey",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                columns: new[] { "TenantId", "ActorType", "ActorId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkWorkflowRuns_TenantId_WorkflowVersionId_Status",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                columns: new[] { "TenantId", "WorkflowVersionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkToolApprovalRequests_TenantId_WorkflowStepRunId",
                schema: "dbo",
                table: "AnkToolApprovalRequests",
                columns: new[] { "TenantId", "WorkflowStepRunId" },
                unique: true,
                filter: "[WorkflowStepRunId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkProposals_TenantId_WorkflowStepRunId",
                schema: "dbo",
                table: "AnkProposals",
                columns: new[] { "TenantId", "WorkflowStepRunId" },
                unique: true,
                filter: "[WorkflowStepRunId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkInvoices_TenantId_IssueOperationId",
                schema: "dbo",
                table: "AnkInvoices",
                columns: new[] { "TenantId", "IssueOperationId" },
                unique: true,
                filter: "[IssueOperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkWorkflowCheckpoints_TenantId_RunId_CheckpointId",
                schema: "dbo",
                table: "AnkWorkflowCheckpoints",
                columns: new[] { "TenantId", "RunId", "CheckpointId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkWorkflowCheckpoints_TenantId_RunId_Sequence",
                schema: "dbo",
                table: "AnkWorkflowCheckpoints",
                columns: new[] { "TenantId", "RunId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkWorkflowStepRuns_TenantId_OperationId",
                schema: "dbo",
                table: "AnkWorkflowStepRuns",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkWorkflowStepRuns_TenantId_RunId_NodeKey_InvocationPath",
                schema: "dbo",
                table: "AnkWorkflowStepRuns",
                columns: new[] { "TenantId", "RunId", "NodeKey", "InvocationPath" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkWorkflowCheckpoints",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkWorkflowStepRuns",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_AnkWorkflowRuns_TenantId_ActorType_ActorId_IdempotencyKey",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropIndex(
                name: "IX_AnkWorkflowRuns_TenantId_WorkflowVersionId_Status",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropIndex(
                name: "IX_AnkToolApprovalRequests_TenantId_WorkflowStepRunId",
                schema: "dbo",
                table: "AnkToolApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_AnkProposals_TenantId_WorkflowStepRunId",
                schema: "dbo",
                table: "AnkProposals");

            migrationBuilder.DropIndex(
                name: "IX_AnkInvoices_TenantId_IssueOperationId",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.DropColumn(
                name: "ActorId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "ActorType",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "CheckpointId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "Continuation",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "DefinitionHash",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "ErrorCode",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "FactsHash",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "FactsJson",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "FrameworkSessionId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "InitiatingUserId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "InputHash",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "LeaseId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "Mode",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "Outcome",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "PendingStepRunId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "SubjectType",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "WorkflowVersionId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "WorkflowStepRunId",
                schema: "dbo",
                table: "AnkToolApprovalRequests");

            migrationBuilder.DropColumn(
                name: "WorkflowStepRunId",
                schema: "dbo",
                table: "AnkProposals");

            migrationBuilder.DropColumn(
                name: "IssueApprovalRequestId",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.DropColumn(
                name: "IssueApprovedByUserId",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.DropColumn(
                name: "IssueOperationId",
                schema: "dbo",
                table: "AnkInvoices");
        }
    }
}

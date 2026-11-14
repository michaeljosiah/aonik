using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomaticWorkflowTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeferredUntilUtc",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CausationRunId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RootRunId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceEventId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceOccurredAtUtc",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TriggerApprovalProposalId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TriggerBindingId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TriggerBindingRevision",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerDefinitionHash",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerPathJson",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "AnkConnectorSubscriptions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectorKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ApplicationKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProviderAccountHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WatchClaim = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BindingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HistoryId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    TargetHistoryId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PageToken = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    FullSync = table.Column<bool>(type: "bit", nullable: false),
                    FullSyncStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FullSyncSinceUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FullSyncPages = table.Column<int>(type: "int", nullable: false),
                    WatchExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WatchRenewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextPollAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSuccessAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PollTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PollTaskProof = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PollTaskStartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PollTaskEndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AnkConnectorSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkWorkflowTriggerBindings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    DraftRevision = table.Column<int>(type: "int", nullable: false),
                    DraftJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DraftHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActiveRevision = table.Column<int>(type: "int", nullable: true),
                    DisabledThroughRevision = table.Column<int>(type: "int", nullable: false),
                    ActiveJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActiveHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ActiveSource = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ActiveAutomationIdentityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfiguredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActivationProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisabledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisabledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdmittedRuns = table.Column<long>(type: "bigint", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
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
                    table.PrimaryKey("PK_AnkWorkflowTriggerBindings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkWorkflowRuns_TenantId_TriggerBindingId_StartedAt",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                columns: new[] { "TenantId", "TriggerBindingId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkWorkflowRuns_TenantId_TriggerBindingId_SubjectId_Status",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                columns: new[] { "TenantId", "TriggerBindingId", "SubjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorSubscriptions_ApplicationKey_ProviderAccountHash_Status",
                schema: "dbo",
                table: "AnkConnectorSubscriptions",
                columns: new[] { "ApplicationKey", "ProviderAccountHash", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorSubscriptions_TenantId_ConnectionId",
                schema: "dbo",
                table: "AnkConnectorSubscriptions",
                columns: new[] { "TenantId", "ConnectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorSubscriptions_WatchClaim",
                schema: "dbo",
                table: "AnkConnectorSubscriptions",
                column: "WatchClaim",
                unique: true,
                filter: "[WatchClaim] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkWorkflowTriggerBindings_TenantId_ActiveAutomationIdentityId_Status",
                schema: "dbo",
                table: "AnkWorkflowTriggerBindings",
                columns: new[] { "TenantId", "ActiveAutomationIdentityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkWorkflowTriggerBindings_TenantId_ActiveSource_Status",
                schema: "dbo",
                table: "AnkWorkflowTriggerBindings",
                columns: new[] { "TenantId", "ActiveSource", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkWorkflowTriggerBindings_TenantId_WorkflowId_Status",
                schema: "dbo",
                table: "AnkWorkflowTriggerBindings",
                columns: new[] { "TenantId", "WorkflowId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkConnectorSubscriptions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkWorkflowTriggerBindings",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_AnkWorkflowRuns_TenantId_TriggerBindingId_StartedAt",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropIndex(
                name: "IX_AnkWorkflowRuns_TenantId_TriggerBindingId_SubjectId_Status",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "DeferredUntilUtc",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "CausationRunId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "RootRunId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "SourceEventId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "SourceOccurredAtUtc",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "TriggerApprovalProposalId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "TriggerBindingId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "TriggerBindingRevision",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "TriggerDefinitionHash",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "TriggerPathJson",
                schema: "dbo",
                table: "AnkWorkflowRuns");
        }
    }
}

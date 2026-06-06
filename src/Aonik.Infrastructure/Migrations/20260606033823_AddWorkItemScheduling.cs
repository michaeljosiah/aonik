using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "ContextType",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "HistoryJson",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.RenameColumn(
                name: "WorkItemType",
                schema: "dbo",
                table: "AnkWorkItems",
                newName: "ActionPayloadJson");

            migrationBuilder.RenameColumn(
                name: "SlaDueAt",
                schema: "dbo",
                table: "AnkWorkItems",
                newName: "StartAtUtc");

            migrationBuilder.RenameColumn(
                name: "AssignedToUserId",
                schema: "dbo",
                table: "AnkWorkItems",
                newName: "SubjectId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "Priority",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "ActionType",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "AssigneeId",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssigneeKey",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssigneeType",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndAtUtc",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeasedBy",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeasedUntilUtc",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxRuns",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRunAtUtc",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecurrenceCron",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RunCount",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ScheduleType",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceModule",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubjectType",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Timezone",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AnkWorkItemRuns",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledForUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AiRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AnkWorkItemRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItem_Assignee",
                schema: "dbo",
                table: "AnkWorkItems",
                columns: new[] { "AssigneeType", "AssigneeId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItem_CorrelationId",
                schema: "dbo",
                table: "AnkWorkItems",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItem_Status_NextRunAtUtc",
                schema: "dbo",
                table: "AnkWorkItems",
                columns: new[] { "Status", "NextRunAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItem_Subject",
                schema: "dbo",
                table: "AnkWorkItems",
                columns: new[] { "SubjectType", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemRun_WorkItemId_ScheduledForUtc",
                schema: "dbo",
                table: "AnkWorkItemRuns",
                columns: new[] { "WorkItemId", "ScheduledForUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkWorkItemRuns",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_WorkItem_Assignee",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItem_CorrelationId",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItem_Status_NextRunAtUtc",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItem_Subject",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "ActionType",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "AssigneeId",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "AssigneeKey",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "AssigneeType",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "EndAtUtc",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "LastError",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "LeasedBy",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "LeasedUntilUtc",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "MaxRuns",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "NextRunAtUtc",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "RecurrenceCron",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "RunCount",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "ScheduleType",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "SourceModule",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "SubjectType",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "Timezone",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.DropColumn(
                name: "Title",
                schema: "dbo",
                table: "AnkWorkItems");

            migrationBuilder.RenameColumn(
                name: "SubjectId",
                schema: "dbo",
                table: "AnkWorkItems",
                newName: "AssignedToUserId");

            migrationBuilder.RenameColumn(
                name: "StartAtUtc",
                schema: "dbo",
                table: "AnkWorkItems",
                newName: "SlaDueAt");

            migrationBuilder.RenameColumn(
                name: "ActionPayloadJson",
                schema: "dbo",
                table: "AnkWorkItems",
                newName: "WorkItemType");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<Guid>(
                name: "ContextId",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ContextType",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HistoryJson",
                schema: "dbo",
                table: "AnkWorkItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}

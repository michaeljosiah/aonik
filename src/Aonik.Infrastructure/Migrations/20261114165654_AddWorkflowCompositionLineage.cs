using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowCompositionLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Depth",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ExecutionRootRunId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvocationPath",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "root");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentRunId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentStepRunId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkWorkflowRuns_TenantId_ExecutionRootRunId_Status",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                columns: new[] { "TenantId", "ExecutionRootRunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkWorkflowRuns_TenantId_ParentStepRunId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                columns: new[] { "TenantId", "ParentStepRunId" },
                unique: true,
                filter: "[ParentStepRunId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkWorkflowRuns_TenantId_ExecutionRootRunId_Status",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropIndex(
                name: "IX_AnkWorkflowRuns_TenantId_ParentStepRunId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "Depth",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "ExecutionRootRunId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "InvocationPath",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "ParentRunId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "ParentStepRunId",
                schema: "dbo",
                table: "AnkWorkflowRuns");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowWaitsAndHumanResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeadlineUtc",
                schema: "dbo",
                table: "AnkWorkflowStepRuns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefinitionSchemaVersion",
                schema: "dbo",
                table: "AnkWorkflows",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadlineUtc",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RuntimeSchemaVersion",
                schema: "dbo",
                table: "AnkWorkflowCheckpoints",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ResponseHash",
                schema: "dbo",
                table: "AnkToolApprovalRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseJson",
                schema: "dbo",
                table: "AnkToolApprovalRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeadlineUtc",
                schema: "dbo",
                table: "AnkWorkflowStepRuns");

            migrationBuilder.DropColumn(
                name: "DefinitionSchemaVersion",
                schema: "dbo",
                table: "AnkWorkflows");

            migrationBuilder.DropColumn(
                name: "DeadlineUtc",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "RuntimeSchemaVersion",
                schema: "dbo",
                table: "AnkWorkflowCheckpoints");

            migrationBuilder.DropColumn(
                name: "ResponseHash",
                schema: "dbo",
                table: "AnkToolApprovalRequests");

            migrationBuilder.DropColumn(
                name: "ResponseJson",
                schema: "dbo",
                table: "AnkToolApprovalRequests");
        }
    }
}

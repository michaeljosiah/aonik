using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionedWorkflowDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AiRunId",
                schema: "dbo",
                table: "AnkWorkflowVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefinitionHash",
                schema: "dbo",
                table: "AnkWorkflowVersions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefinitionJson",
                schema: "dbo",
                table: "AnkWorkflowVersions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                schema: "dbo",
                table: "AnkWorkflowVersions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedByUserId",
                schema: "dbo",
                table: "AnkWorkflowVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                schema: "dbo",
                table: "AnkWorkflowVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefinitionKind",
                schema: "dbo",
                table: "AnkWorkflows",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Workflow");

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedVersionId",
                schema: "dbo",
                table: "AnkWorkflows",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAt",
                schema: "dbo",
                table: "AnkWorkflows",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RetiredByUserId",
                schema: "dbo",
                table: "AnkWorkflows",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogicalKey",
                schema: "dbo",
                table: "AnkWorkflowNodes",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<string>(
                name: "RouteKey",
                schema: "dbo",
                table: "AnkWorkflowEdges",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkWorkflowVersions_TenantId_WorkflowId_PublishedAt",
                schema: "dbo",
                table: "AnkWorkflowVersions",
                columns: new[] { "TenantId", "WorkflowId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkWorkflowNodes_TenantId_WorkflowId_LogicalKey",
                schema: "dbo",
                table: "AnkWorkflowNodes",
                columns: new[] { "TenantId", "WorkflowId", "LogicalKey" },
                unique: true,
                filter: "[LogicalKey] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkWorkflowVersions_TenantId_WorkflowId_PublishedAt",
                schema: "dbo",
                table: "AnkWorkflowVersions");

            migrationBuilder.DropIndex(
                name: "IX_AnkWorkflowNodes_TenantId_WorkflowId_LogicalKey",
                schema: "dbo",
                table: "AnkWorkflowNodes");

            migrationBuilder.DropColumn(
                name: "AiRunId",
                schema: "dbo",
                table: "AnkWorkflowVersions");

            migrationBuilder.DropColumn(
                name: "DefinitionHash",
                schema: "dbo",
                table: "AnkWorkflowVersions");

            migrationBuilder.DropColumn(
                name: "DefinitionJson",
                schema: "dbo",
                table: "AnkWorkflowVersions");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                schema: "dbo",
                table: "AnkWorkflowVersions");

            migrationBuilder.DropColumn(
                name: "PublishedByUserId",
                schema: "dbo",
                table: "AnkWorkflowVersions");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                schema: "dbo",
                table: "AnkWorkflowVersions");

            migrationBuilder.DropColumn(
                name: "DefinitionKind",
                schema: "dbo",
                table: "AnkWorkflows");

            migrationBuilder.DropColumn(
                name: "PublishedVersionId",
                schema: "dbo",
                table: "AnkWorkflows");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                schema: "dbo",
                table: "AnkWorkflows");

            migrationBuilder.DropColumn(
                name: "RetiredByUserId",
                schema: "dbo",
                table: "AnkWorkflows");

            migrationBuilder.DropColumn(
                name: "LogicalKey",
                schema: "dbo",
                table: "AnkWorkflowNodes");

            migrationBuilder.DropColumn(
                name: "RouteKey",
                schema: "dbo",
                table: "AnkWorkflowEdges");
        }
    }
}

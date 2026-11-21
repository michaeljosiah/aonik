using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantAutomationIdentities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConfiguredByUserId",
                schema: "dbo",
                table: "AnkWorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AutomationGrantId",
                schema: "dbo",
                table: "AnkConnectorConnectionRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AutomationIdentityId",
                schema: "dbo",
                table: "AnkConnectorConnectionRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnkAutomationIdentities",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    AccountableUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfiguredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisabledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisabledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GrantsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PermissionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_AnkAutomationIdentities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkAutomationIdentities_TenantId_Status_ExpiresAt",
                schema: "dbo",
                table: "AnkAutomationIdentities",
                columns: new[] { "TenantId", "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkAutomationIdentities",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "ConfiguredByUserId",
                schema: "dbo",
                table: "AnkWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "AutomationGrantId",
                schema: "dbo",
                table: "AnkConnectorConnectionRequests");

            migrationBuilder.DropColumn(
                name: "AutomationIdentityId",
                schema: "dbo",
                table: "AnkConnectorConnectionRequests");
        }
    }
}

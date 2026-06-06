using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantAgentExtensibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkTenantHttpTools",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UrlTemplate = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ParameterSchemaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthKind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProtectedAuthJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RiskTier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActionKind = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProposalType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovalState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CredentialVersion = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AnkTenantHttpTools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkTenantMcpServers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    TransportType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AuthKind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProtectedAuthJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AllowedToolPrefixesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultRiskTier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApprovalState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CredentialVersion = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AnkTenantMcpServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkTenantSkills",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    FrontmatterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllowedToolsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScriptsPresent = table.Column<bool>(type: "bit", nullable: false),
                    ScriptsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AnkTenantSkills", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantHttpTool_Tenant_Active_State",
                schema: "dbo",
                table: "AnkTenantHttpTools",
                columns: new[] { "TenantId", "IsActive", "ApprovalState" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantHttpTool_Tenant_Name",
                schema: "dbo",
                table: "AnkTenantHttpTools",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantMcpServer_Tenant_Active_State",
                schema: "dbo",
                table: "AnkTenantMcpServers",
                columns: new[] { "TenantId", "IsActive", "ApprovalState" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantMcpServer_Tenant_Name",
                schema: "dbo",
                table: "AnkTenantMcpServers",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSkill_Tenant_Active_State",
                schema: "dbo",
                table: "AnkTenantSkills",
                columns: new[] { "TenantId", "IsActive", "ApprovalState" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantSkill_Tenant_Name",
                schema: "dbo",
                table: "AnkTenantSkills",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkTenantHttpTools",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkTenantMcpServers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkTenantSkills",
                schema: "dbo");
        }
    }
}

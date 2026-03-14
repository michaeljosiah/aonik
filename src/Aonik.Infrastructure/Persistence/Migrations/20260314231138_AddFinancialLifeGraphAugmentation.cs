using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialLifeGraphAugmentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkFinancialLifeGraphEdges",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FromNodeKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Predicate = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ToNodeKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PropertiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsInferred = table.Column<bool>(type: "bit", nullable: false),
                    AiRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnkFinancialLifeGraphEdges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkFinancialLifeGraphNodes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NodeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceEntity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PropertiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsInferred = table.Column<bool>(type: "bit", nullable: false),
                    AiRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnkFinancialLifeGraphNodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkFinancialLifeGraphEdges_TenantId_UserId_FromNodeKey_Predicate",
                schema: "dbo",
                table: "AnkFinancialLifeGraphEdges",
                columns: new[] { "TenantId", "UserId", "FromNodeKey", "Predicate" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkFinancialLifeGraphEdges_TenantId_UserId_HouseholdId",
                schema: "dbo",
                table: "AnkFinancialLifeGraphEdges",
                columns: new[] { "TenantId", "UserId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkFinancialLifeGraphEdges_TenantId_UserId_Status",
                schema: "dbo",
                table: "AnkFinancialLifeGraphEdges",
                columns: new[] { "TenantId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkFinancialLifeGraphEdges_TenantId_UserId_ToNodeKey_Predicate",
                schema: "dbo",
                table: "AnkFinancialLifeGraphEdges",
                columns: new[] { "TenantId", "UserId", "ToNodeKey", "Predicate" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkFinancialLifeGraphNodes_TenantId_UserId_HouseholdId",
                schema: "dbo",
                table: "AnkFinancialLifeGraphNodes",
                columns: new[] { "TenantId", "UserId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkFinancialLifeGraphNodes_TenantId_UserId_NodeType",
                schema: "dbo",
                table: "AnkFinancialLifeGraphNodes",
                columns: new[] { "TenantId", "UserId", "NodeType" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkFinancialLifeGraphNodes_TenantId_UserId_SourceEntity_SourceId",
                schema: "dbo",
                table: "AnkFinancialLifeGraphNodes",
                columns: new[] { "TenantId", "UserId", "SourceEntity", "SourceId" },
                filter: "[SourceEntity] IS NOT NULL AND [SourceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkFinancialLifeGraphNodes_TenantId_UserId_Status",
                schema: "dbo",
                table: "AnkFinancialLifeGraphNodes",
                columns: new[] { "TenantId", "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkFinancialLifeGraphEdges",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkFinancialLifeGraphNodes",
                schema: "dbo");
        }
    }
}

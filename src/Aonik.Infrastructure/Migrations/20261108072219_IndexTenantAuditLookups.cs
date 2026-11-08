using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IndexTenantAuditLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AnkAuditLogs_TenantId_IsDeleted_CorrelationId_Timestamp",
                schema: "dbo",
                table: "AnkAuditLogs",
                columns: new[] { "TenantId", "IsDeleted", "CorrelationId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkAuditLogs_TenantId_IsDeleted_ResourceId_Timestamp",
                schema: "dbo",
                table: "AnkAuditLogs",
                columns: new[] { "TenantId", "IsDeleted", "ResourceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkAuditLogs_TenantId_IsDeleted_Timestamp",
                schema: "dbo",
                table: "AnkAuditLogs",
                columns: new[] { "TenantId", "IsDeleted", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkAuditLogs_TenantId_IsDeleted_CorrelationId_Timestamp",
                schema: "dbo",
                table: "AnkAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AnkAuditLogs_TenantId_IsDeleted_ResourceId_Timestamp",
                schema: "dbo",
                table: "AnkAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AnkAuditLogs_TenantId_IsDeleted_Timestamp",
                schema: "dbo",
                table: "AnkAuditLogs");
        }
    }
}

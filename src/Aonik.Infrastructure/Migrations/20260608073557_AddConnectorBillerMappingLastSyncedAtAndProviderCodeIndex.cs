using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectorBillerMappingLastSyncedAtAndProviderCodeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAt",
                schema: "dbo",
                table: "AnkConnectorBillerMappings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorBillerMappings_TenantId_ConnectorId_ProviderBillerCode_ProviderItemCode",
                schema: "dbo",
                table: "AnkConnectorBillerMappings",
                columns: new[] { "TenantId", "ConnectorId", "ProviderBillerCode", "ProviderItemCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkConnectorBillerMappings_TenantId_ConnectorId_ProviderBillerCode_ProviderItemCode",
                schema: "dbo",
                table: "AnkConnectorBillerMappings");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                schema: "dbo",
                table: "AnkConnectorBillerMappings");
        }
    }
}

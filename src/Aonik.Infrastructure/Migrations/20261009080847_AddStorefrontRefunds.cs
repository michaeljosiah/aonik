using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStorefrontRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EffectsAppliedAtUtc",
                schema: "dbo",
                table: "AnkRefunds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GiftReclaimAmount",
                schema: "dbo",
                table: "AnkRefunds",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "GiftReclaimCardId",
                schema: "dbo",
                table: "AnkRefunds",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GiftReclaimReleasedAtUtc",
                schema: "dbo",
                table: "AnkRefunds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderRequestStartedAtUtc",
                schema: "dbo",
                table: "AnkRefunds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestSnapshotJson",
                schema: "dbo",
                table: "AnkRefunds",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalOperationId",
                schema: "dbo",
                table: "AnkGiftCardOperations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkRefunds_TenantId_ConnectorId_ProviderReference",
                schema: "dbo",
                table: "AnkRefunds",
                columns: new[] { "TenantId", "ConnectorId", "ProviderReference" },
                unique: true,
                filter: "[ProviderReference] IS NOT NULL AND [ConnectorId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AnkRefunds_TenantId_GiftReclaimCardId",
                schema: "dbo",
                table: "AnkRefunds",
                columns: new[] { "TenantId", "GiftReclaimCardId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCardOperations_TenantId_OriginalOperationId",
                schema: "dbo",
                table: "AnkGiftCardOperations",
                columns: new[] { "TenantId", "OriginalOperationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkRefunds_TenantId_ConnectorId_ProviderReference",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropIndex(
                name: "IX_AnkRefunds_TenantId_GiftReclaimCardId",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropIndex(
                name: "IX_AnkGiftCardOperations_TenantId_OriginalOperationId",
                schema: "dbo",
                table: "AnkGiftCardOperations");

            migrationBuilder.DropColumn(
                name: "EffectsAppliedAtUtc",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropColumn(
                name: "GiftReclaimAmount",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropColumn(
                name: "GiftReclaimCardId",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropColumn(
                name: "GiftReclaimReleasedAtUtc",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropColumn(
                name: "ProviderRequestStartedAtUtc",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropColumn(
                name: "RequestSnapshotJson",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropColumn(
                name: "OriginalOperationId",
                schema: "dbo",
                table: "AnkGiftCardOperations");
        }
    }
}

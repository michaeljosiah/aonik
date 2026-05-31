using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyIndexesAndOutboxLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkOrders_IdempotencyKey",
                schema: "dbo",
                table: "AnkOrders");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimExpiresAt",
                schema: "dbo",
                table: "AnkOutboxMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                schema: "dbo",
                table: "AnkOutboxMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimedBy",
                schema: "dbo",
                table: "AnkOutboxMessages",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrders_TenantId_OrderType_IdempotencyKey",
                schema: "dbo",
                table: "AnkOrders",
                columns: new[] { "TenantId", "OrderType", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkJournalEntries_TenantId_SourceType_SourceId",
                schema: "dbo",
                table: "AnkJournalEntries",
                columns: new[] { "TenantId", "SourceType", "SourceId" },
                unique: true,
                filter: "[SourceType] <> 'Manual'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkOrders_TenantId_OrderType_IdempotencyKey",
                schema: "dbo",
                table: "AnkOrders");

            migrationBuilder.DropIndex(
                name: "IX_AnkJournalEntries_TenantId_SourceType_SourceId",
                schema: "dbo",
                table: "AnkJournalEntries");

            migrationBuilder.DropColumn(
                name: "ClaimExpiresAt",
                schema: "dbo",
                table: "AnkOutboxMessages");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                schema: "dbo",
                table: "AnkOutboxMessages");

            migrationBuilder.DropColumn(
                name: "ClaimedBy",
                schema: "dbo",
                table: "AnkOutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrders_IdempotencyKey",
                schema: "dbo",
                table: "AnkOrders",
                column: "IdempotencyKey");
        }
    }
}

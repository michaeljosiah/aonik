using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStorefrontOrderHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkOrders_OrderNumber",
                schema: "dbo",
                table: "AnkOrders");

            migrationBuilder.AddColumn<string>(
                name: "NameSnapshot",
                schema: "dbo",
                table: "AnkOrderItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedTermsUrl",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedTermsVersion",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfilmentHistoryJson",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfilmentStatus",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TermsAcceptedAtUtc",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSignatureSnapshot",
                schema: "dbo",
                table: "AnkOrderBundleSelections",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameSnapshot",
                schema: "dbo",
                table: "AnkOrderBundleSelections",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrders_TenantId_OrderNumber",
                schema: "dbo",
                table: "AnkOrders",
                columns: new[] { "TenantId", "OrderNumber" },
                unique: true,
                filter: "[OrderNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkOrders_TenantId_OrderNumber",
                schema: "dbo",
                table: "AnkOrders");

            migrationBuilder.DropColumn(
                name: "NameSnapshot",
                schema: "dbo",
                table: "AnkOrderItems");

            migrationBuilder.DropColumn(
                name: "AcceptedTermsUrl",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails");

            migrationBuilder.DropColumn(
                name: "AcceptedTermsVersion",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails");

            migrationBuilder.DropColumn(
                name: "FulfilmentHistoryJson",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails");

            migrationBuilder.DropColumn(
                name: "FulfilmentStatus",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAtUtc",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails");

            migrationBuilder.DropColumn(
                name: "IsSignatureSnapshot",
                schema: "dbo",
                table: "AnkOrderBundleSelections");

            migrationBuilder.DropColumn(
                name: "NameSnapshot",
                schema: "dbo",
                table: "AnkOrderBundleSelections");

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrders_OrderNumber",
                schema: "dbo",
                table: "AnkOrders",
                column: "OrderNumber",
                unique: true,
                filter: "[OrderNumber] IS NOT NULL");
        }
    }
}

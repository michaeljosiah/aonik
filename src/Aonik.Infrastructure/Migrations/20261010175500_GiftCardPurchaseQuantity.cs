using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GiftCardPurchaseQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkOrderGiftCardDeliveries_TenantId_PaymentIntentId",
                schema: "dbo",
                table: "AnkOrderGiftCardDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_AnkGiftCards_TenantId_CartId",
                schema: "dbo",
                table: "AnkGiftCards");

            migrationBuilder.DropIndex(
                name: "IX_AnkGiftCards_TenantId_PaymentIntentId",
                schema: "dbo",
                table: "AnkGiftCards");

            migrationBuilder.DropIndex(
                name: "IX_AnkGiftCardOperations_TenantId_Kind_SourceId",
                schema: "dbo",
                table: "AnkGiftCardOperations");

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrderGiftCardDeliveries_TenantId_PaymentIntentId_OrderItemId",
                schema: "dbo",
                table: "AnkOrderGiftCardDeliveries",
                columns: new[] { "TenantId", "PaymentIntentId", "OrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCards_TenantId_CartId_OrderItemId",
                schema: "dbo",
                table: "AnkGiftCards",
                columns: new[] { "TenantId", "CartId", "OrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCards_TenantId_PaymentIntentId_OrderItemId",
                schema: "dbo",
                table: "AnkGiftCards",
                columns: new[] { "TenantId", "PaymentIntentId", "OrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCardOperations_TenantId_Kind_SourceId_GiftCardId",
                schema: "dbo",
                table: "AnkGiftCardOperations",
                columns: new[] { "TenantId", "Kind", "SourceId", "GiftCardId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkOrderGiftCardDeliveries_TenantId_PaymentIntentId_OrderItemId",
                schema: "dbo",
                table: "AnkOrderGiftCardDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_AnkGiftCards_TenantId_CartId_OrderItemId",
                schema: "dbo",
                table: "AnkGiftCards");

            migrationBuilder.DropIndex(
                name: "IX_AnkGiftCards_TenantId_PaymentIntentId_OrderItemId",
                schema: "dbo",
                table: "AnkGiftCards");

            migrationBuilder.DropIndex(
                name: "IX_AnkGiftCardOperations_TenantId_Kind_SourceId_GiftCardId",
                schema: "dbo",
                table: "AnkGiftCardOperations");

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrderGiftCardDeliveries_TenantId_PaymentIntentId",
                schema: "dbo",
                table: "AnkOrderGiftCardDeliveries",
                columns: new[] { "TenantId", "PaymentIntentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCards_TenantId_CartId",
                schema: "dbo",
                table: "AnkGiftCards",
                columns: new[] { "TenantId", "CartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCards_TenantId_PaymentIntentId",
                schema: "dbo",
                table: "AnkGiftCards",
                columns: new[] { "TenantId", "PaymentIntentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCardOperations_TenantId_Kind_SourceId",
                schema: "dbo",
                table: "AnkGiftCardOperations",
                columns: new[] { "TenantId", "Kind", "SourceId" },
                unique: true);
        }
    }
}

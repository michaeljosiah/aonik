using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceAndIntentIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkInvoices_TenantId_OrderId",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "dbo",
                table: "AnkInvoices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentIntents_TenantId_IdempotencyKey",
                schema: "dbo",
                table: "AnkPaymentIntents",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkInvoices_TenantId_IdempotencyKey",
                schema: "dbo",
                table: "AnkInvoices",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkInvoices_TenantId_OrderId",
                schema: "dbo",
                table: "AnkInvoices",
                columns: new[] { "TenantId", "OrderId" },
                unique: true,
                filter: "[OrderId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkPaymentIntents_TenantId_IdempotencyKey",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropIndex(
                name: "IX_AnkInvoices_TenantId_IdempotencyKey",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.DropIndex(
                name: "IX_AnkInvoices_TenantId_OrderId",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.CreateIndex(
                name: "IX_AnkInvoices_TenantId_OrderId",
                schema: "dbo",
                table: "AnkInvoices",
                columns: new[] { "TenantId", "OrderId" });
        }
    }
}

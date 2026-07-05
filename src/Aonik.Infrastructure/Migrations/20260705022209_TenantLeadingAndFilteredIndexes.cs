using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantLeadingAndFilteredIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkPaymentIntents_InvoiceId",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropIndex(
                name: "IX_AnkPaymentIntents_OrderId",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropIndex(
                name: "IX_AnkPaymentIntents_PayerPartyId",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropIndex(
                name: "IX_AnkPaymentIntents_Status",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropIndex(
                name: "IX_AnkOutboxMessages_Dispatch",
                schema: "dbo",
                table: "AnkOutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_AnkInvoices_CustomerAccountId",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.DropIndex(
                name: "IX_AnkInvoices_DueDate",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.DropIndex(
                name: "IX_AnkInvoices_OrderId",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.DropIndex(
                name: "IX_AnkInvoices_Status",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentIntents_TenantId_InvoiceId",
                schema: "dbo",
                table: "AnkPaymentIntents",
                columns: new[] { "TenantId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentIntents_TenantId_OrderId",
                schema: "dbo",
                table: "AnkPaymentIntents",
                columns: new[] { "TenantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentIntents_TenantId_PayerPartyId",
                schema: "dbo",
                table: "AnkPaymentIntents",
                columns: new[] { "TenantId", "PayerPartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentIntents_TenantId_Status",
                schema: "dbo",
                table: "AnkPaymentIntents",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkOutboxMessages_Dispatch",
                schema: "dbo",
                table: "AnkOutboxMessages",
                columns: new[] { "NextAttemptAt", "CreatedAt" },
                filter: "[ProcessedAt] IS NULL AND [DeadLetteredAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkInvoices_TenantId_CustomerAccountId",
                schema: "dbo",
                table: "AnkInvoices",
                columns: new[] { "TenantId", "CustomerAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkInvoices_TenantId_DueDate",
                schema: "dbo",
                table: "AnkInvoices",
                columns: new[] { "TenantId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkInvoices_TenantId_OrderId",
                schema: "dbo",
                table: "AnkInvoices",
                columns: new[] { "TenantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkInvoices_TenantId_Status",
                schema: "dbo",
                table: "AnkInvoices",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkPaymentIntents_TenantId_InvoiceId",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropIndex(
                name: "IX_AnkPaymentIntents_TenantId_OrderId",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropIndex(
                name: "IX_AnkPaymentIntents_TenantId_PayerPartyId",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropIndex(
                name: "IX_AnkPaymentIntents_TenantId_Status",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropIndex(
                name: "IX_AnkOutboxMessages_Dispatch",
                schema: "dbo",
                table: "AnkOutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_AnkInvoices_TenantId_CustomerAccountId",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.DropIndex(
                name: "IX_AnkInvoices_TenantId_DueDate",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.DropIndex(
                name: "IX_AnkInvoices_TenantId_OrderId",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.DropIndex(
                name: "IX_AnkInvoices_TenantId_Status",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentIntents_InvoiceId",
                schema: "dbo",
                table: "AnkPaymentIntents",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentIntents_OrderId",
                schema: "dbo",
                table: "AnkPaymentIntents",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentIntents_PayerPartyId",
                schema: "dbo",
                table: "AnkPaymentIntents",
                column: "PayerPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentIntents_Status",
                schema: "dbo",
                table: "AnkPaymentIntents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AnkOutboxMessages_Dispatch",
                schema: "dbo",
                table: "AnkOutboxMessages",
                columns: new[] { "ProcessedAt", "DeadLetteredAt", "NextAttemptAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkInvoices_CustomerAccountId",
                schema: "dbo",
                table: "AnkInvoices",
                column: "CustomerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkInvoices_DueDate",
                schema: "dbo",
                table: "AnkInvoices",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_AnkInvoices_OrderId",
                schema: "dbo",
                table: "AnkInvoices",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkInvoices_Status",
                schema: "dbo",
                table: "AnkInvoices",
                column: "Status");
        }
    }
}

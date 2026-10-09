using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkOrderChargeSummaries_TenantId_OrderId",
                schema: "dbo",
                table: "AnkOrderChargeSummaries");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderReference",
                schema: "dbo",
                table: "AnkPayments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                schema: "dbo",
                table: "AnkPayments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "OutcomeStatus",
                schema: "dbo",
                table: "AnkPayments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                schema: "dbo",
                table: "AnkPayments",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConnectorId",
                schema: "dbo",
                table: "AnkPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "AnkPayments",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderAccountId",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderCode",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderCreateRequestJson",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProviderLiveMode",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPaymentIntentReference",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderRequestStartedAtUtc",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DiscountId",
                schema: "dbo",
                table: "AnkOrderChargeSummaries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutPreparationJson",
                schema: "dbo",
                table: "AnkCarts",
                type: "nvarchar(max)",
                maxLength: 262144,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutState",
                schema: "dbo",
                table: "AnkCarts",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkPayments_ConnectorId_ProviderReference",
                schema: "dbo",
                table: "AnkPayments",
                columns: new[] { "ConnectorId", "ProviderReference" },
                unique: true,
                filter: "[ConnectorId] IS NOT NULL AND [ProviderReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPayments_TenantId_PaymentIntentId",
                schema: "dbo",
                table: "AnkPayments",
                columns: new[] { "TenantId", "PaymentIntentId" },
                unique: true,
                filter: "[ConnectorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentIntents_ConnectorId_ProviderPaymentIntentReference",
                schema: "dbo",
                table: "AnkPaymentIntents",
                columns: new[] { "ConnectorId", "ProviderPaymentIntentReference" },
                unique: true,
                filter: "[ConnectorId] IS NOT NULL AND [ProviderPaymentIntentReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentIntents_ConnectorId_ProviderReference",
                schema: "dbo",
                table: "AnkPaymentIntents",
                columns: new[] { "ConnectorId", "ProviderReference" },
                unique: true,
                filter: "[ConnectorId] IS NOT NULL AND [ProviderCode] = 'Stripe' AND [ProviderReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrderChargeSummaries_TenantId_OrderId",
                schema: "dbo",
                table: "AnkOrderChargeSummaries",
                columns: new[] { "TenantId", "OrderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkPayments_ConnectorId_ProviderReference",
                schema: "dbo",
                table: "AnkPayments");

            migrationBuilder.DropIndex(
                name: "IX_AnkPayments_TenantId_PaymentIntentId",
                schema: "dbo",
                table: "AnkPayments");

            migrationBuilder.DropIndex(
                name: "IX_AnkPaymentIntents_ConnectorId_ProviderPaymentIntentReference",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropIndex(
                name: "IX_AnkPaymentIntents_ConnectorId_ProviderReference",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropIndex(
                name: "IX_AnkOrderChargeSummaries_TenantId_OrderId",
                schema: "dbo",
                table: "AnkOrderChargeSummaries");

            migrationBuilder.DropColumn(
                name: "Amount",
                schema: "dbo",
                table: "AnkPayments");

            migrationBuilder.DropColumn(
                name: "ConnectorId",
                schema: "dbo",
                table: "AnkPayments");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "dbo",
                table: "AnkPayments");

            migrationBuilder.DropColumn(
                name: "ProviderAccountId",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "ProviderCode",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "ProviderCreateRequestJson",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "ProviderLiveMode",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "ProviderPaymentIntentReference",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "ProviderRequestStartedAtUtc",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "DiscountId",
                schema: "dbo",
                table: "AnkOrderChargeSummaries");

            migrationBuilder.DropColumn(
                name: "CheckoutPreparationJson",
                schema: "dbo",
                table: "AnkCarts");

            migrationBuilder.DropColumn(
                name: "CheckoutState",
                schema: "dbo",
                table: "AnkCarts");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderReference",
                schema: "dbo",
                table: "AnkPayments",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                schema: "dbo",
                table: "AnkPayments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "OutcomeStatus",
                schema: "dbo",
                table: "AnkPayments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrderChargeSummaries_TenantId_OrderId",
                schema: "dbo",
                table: "AnkOrderChargeSummaries",
                columns: new[] { "TenantId", "OrderId" });
        }
    }
}

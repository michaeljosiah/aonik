using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPeriodFulfilmentRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderFulfilmentRefs_ExactlyOneTarget",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs");

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionPeriodId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderFulfilmentRefs_ExactlyOneTarget",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                sql: "(CASE WHEN [PayoutId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [PaymentIntentId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [PartnerBillPaymentId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [SubscriptionPeriodId] IS NOT NULL THEN 1 ELSE 0 END) = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderFulfilmentRefs_ExactlyOneTarget",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs");

            migrationBuilder.DropColumn(
                name: "SubscriptionPeriodId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderFulfilmentRefs_ExactlyOneTarget",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                sql: "(CASE WHEN [PayoutId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [PaymentIntentId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [PartnerBillPaymentId] IS NOT NULL THEN 1 ELSE 0 END) = 1");
        }
    }
}

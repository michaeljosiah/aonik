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
            // Refuses, rather than corrupting.
            //
            // Dropping SubscriptionPeriodId leaves every row that used it with all four target
            // columns null, and the AddCheckConstraint that follows — "exactly one target" — then
            // rejects those existing rows, so the rollback cannot complete once a single renewal has
            // been fulfilled. Deleting the offending rows instead would destroy the fulfilment trace
            // of orders that really were served, which is the one fact the link exists to keep.
            //
            // Throwing before EF changes anything is the only honest answer: a partially applied
            // Down would leave the constraint dropped and the column half-gone, and a no-op Down
            // would make EF delete the history row while the schema stayed.
            //
            // To reverse deliberately, while accepting the loss of those traces:
            //   1. SELECT * FROM dbo.AnkOrderFulfilmentRefs WHERE SubscriptionPeriodId IS NOT NULL
            //   2. decide what becomes of each — there is no other column that can hold them
            //   3. delete them, drop the constraint, drop the column, recreate the three-target constraint
            //   4. delete this migration's row from __EFMigrationsHistory
            throw new InvalidOperationException(
                "AddSubscriptionPeriodFulfilmentRef is forward-only once a subscription period has fulfilled "
                + "an order: dropping the column would leave those rows with no target and the restored check "
                + "constraint would reject them. See the comment in this migration for the manual steps.");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommerceCheckoutIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                schema: "dbo",
                table: "AnkOrderChargeSummaries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentIntentId",
                schema: "dbo",
                table: "AnkOrderChargeSummaries",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                schema: "dbo",
                table: "AnkOrderChargeSummaries",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvoiceId",
                schema: "dbo",
                table: "AnkOrderChargeSummaries");

            migrationBuilder.DropColumn(
                name: "PaymentIntentId",
                schema: "dbo",
                table: "AnkOrderChargeSummaries");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                schema: "dbo",
                table: "AnkOrderChargeSummaries");
        }
    }
}

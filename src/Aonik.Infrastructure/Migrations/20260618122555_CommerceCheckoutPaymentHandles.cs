using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommerceCheckoutPaymentHandles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentCheckoutUrl",
                schema: "dbo",
                table: "AnkOrderChargeSummaries",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentClientSecret",
                schema: "dbo",
                table: "AnkOrderChargeSummaries",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentCheckoutUrl",
                schema: "dbo",
                table: "AnkOrderChargeSummaries");

            migrationBuilder.DropColumn(
                name: "PaymentClientSecret",
                schema: "dbo",
                table: "AnkOrderChargeSummaries");
        }
    }
}

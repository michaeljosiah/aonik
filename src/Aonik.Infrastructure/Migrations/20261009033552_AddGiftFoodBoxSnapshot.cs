using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGiftFoodBoxSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GreetingCardMessage",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HidePrices",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeGreetingCard",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsGift",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "GreetingCardCharged",
                schema: "dbo",
                table: "AnkOrderChargeSummaries",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GreetingCardMessage",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails");

            migrationBuilder.DropColumn(
                name: "HidePrices",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails");

            migrationBuilder.DropColumn(
                name: "IncludeGreetingCard",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails");

            migrationBuilder.DropColumn(
                name: "IsGift",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails");

            migrationBuilder.DropColumn(
                name: "GreetingCardCharged",
                schema: "dbo",
                table: "AnkOrderChargeSummaries");
        }
    }
}

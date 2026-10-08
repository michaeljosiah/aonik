using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCartCheckoutDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckoutDraftJson",
                schema: "dbo",
                table: "AnkCarts",
                type: "nvarchar(max)",
                maxLength: 24000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAtUtc",
                schema: "dbo",
                table: "AnkCarts",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckoutDraftJson",
                schema: "dbo",
                table: "AnkCarts");

            migrationBuilder.DropColumn(
                name: "LastActivityAtUtc",
                schema: "dbo",
                table: "AnkCarts");
        }
    }
}

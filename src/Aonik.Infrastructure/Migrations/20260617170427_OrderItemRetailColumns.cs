using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrderItemRetailColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                schema: "dbo",
                table: "AnkOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                schema: "dbo",
                table: "AnkOrderItems",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                schema: "dbo",
                table: "AnkOrderItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                schema: "dbo",
                table: "AnkOrderItems",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrderItems_ProductId",
                schema: "dbo",
                table: "AnkOrderItems",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkOrderItems_ProductId",
                schema: "dbo",
                table: "AnkOrderItems");

            migrationBuilder.DropColumn(
                name: "ProductId",
                schema: "dbo",
                table: "AnkOrderItems");

            migrationBuilder.DropColumn(
                name: "Quantity",
                schema: "dbo",
                table: "AnkOrderItems");

            migrationBuilder.DropColumn(
                name: "Sku",
                schema: "dbo",
                table: "AnkOrderItems");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                schema: "dbo",
                table: "AnkOrderItems");
        }
    }
}

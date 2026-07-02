using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrderDemandWindowIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AnkOrders_TenantId_OrderType_CreatedAt",
                schema: "dbo",
                table: "AnkOrders",
                columns: new[] { "TenantId", "OrderType", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkOrders_TenantId_OrderType_CreatedAt",
                schema: "dbo",
                table: "AnkOrders");
        }
    }
}

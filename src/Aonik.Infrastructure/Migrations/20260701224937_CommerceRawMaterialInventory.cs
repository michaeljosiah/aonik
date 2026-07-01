using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommerceRawMaterialInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkInventoryLevels_TenantId_ProductVariantId_Location",
                schema: "dbo",
                table: "AnkInventoryLevels");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductVariantId",
                schema: "dbo",
                table: "AnkInventoryReservations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "CartId",
                schema: "dbo",
                table: "AnkInventoryReservations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "IngredientId",
                schema: "dbo",
                table: "AnkInventoryReservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockItemKind",
                schema: "dbo",
                table: "AnkInventoryReservations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "ProductVariant");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductVariantId",
                schema: "dbo",
                table: "AnkInventoryLevels",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "IngredientId",
                schema: "dbo",
                table: "AnkInventoryLevels",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReorderPoint",
                schema: "dbo",
                table: "AnkInventoryLevels",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReorderQuantity",
                schema: "dbo",
                table: "AnkInventoryLevels",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockItemKind",
                schema: "dbo",
                table: "AnkInventoryLevels",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "ProductVariant");

            migrationBuilder.CreateTable(
                name: "AnkLowStockAlerts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AvailableAtRaise = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ReorderPoint = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RaisedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnkLowStockAlerts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkInventoryReservations_IngredientId",
                schema: "dbo",
                table: "AnkInventoryReservations",
                column: "IngredientId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryReservations_ExactlyOneStockItem",
                schema: "dbo",
                table: "AnkInventoryReservations",
                sql: "([ProductVariantId] IS NOT NULL AND [IngredientId] IS NULL AND [StockItemKind] = N'ProductVariant') OR ([ProductVariantId] IS NULL AND [IngredientId] IS NOT NULL AND [StockItemKind] = N'Ingredient')");

            migrationBuilder.CreateIndex(
                name: "IX_AnkInventoryLevels_TenantId_IngredientId_Location",
                schema: "dbo",
                table: "AnkInventoryLevels",
                columns: new[] { "TenantId", "IngredientId", "Location" },
                unique: true,
                filter: "[IngredientId] IS NOT NULL AND [Location] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkInventoryLevels_TenantId_ProductVariantId_Location",
                schema: "dbo",
                table: "AnkInventoryLevels",
                columns: new[] { "TenantId", "ProductVariantId", "Location" },
                unique: true,
                filter: "[ProductVariantId] IS NOT NULL AND [Location] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryLevels_ExactlyOneStockItem",
                schema: "dbo",
                table: "AnkInventoryLevels",
                sql: "([ProductVariantId] IS NOT NULL AND [IngredientId] IS NULL AND [StockItemKind] = N'ProductVariant') OR ([ProductVariantId] IS NULL AND [IngredientId] IS NOT NULL AND [StockItemKind] = N'Ingredient')");

            migrationBuilder.CreateIndex(
                name: "IX_AnkLowStockAlerts_TenantId_IngredientId",
                schema: "dbo",
                table: "AnkLowStockAlerts",
                columns: new[] { "TenantId", "IngredientId" },
                unique: true,
                filter: "[Status] IN (N'Open', N'Acknowledged')");

            migrationBuilder.CreateIndex(
                name: "IX_AnkLowStockAlerts_TenantId_Status_RaisedAt",
                schema: "dbo",
                table: "AnkLowStockAlerts",
                columns: new[] { "TenantId", "Status", "RaisedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkLowStockAlerts",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_AnkInventoryReservations_IngredientId",
                schema: "dbo",
                table: "AnkInventoryReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryReservations_ExactlyOneStockItem",
                schema: "dbo",
                table: "AnkInventoryReservations");

            migrationBuilder.DropIndex(
                name: "IX_AnkInventoryLevels_TenantId_IngredientId_Location",
                schema: "dbo",
                table: "AnkInventoryLevels");

            migrationBuilder.DropIndex(
                name: "IX_AnkInventoryLevels_TenantId_ProductVariantId_Location",
                schema: "dbo",
                table: "AnkInventoryLevels");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryLevels_ExactlyOneStockItem",
                schema: "dbo",
                table: "AnkInventoryLevels");

            migrationBuilder.DropColumn(
                name: "IngredientId",
                schema: "dbo",
                table: "AnkInventoryReservations");

            migrationBuilder.DropColumn(
                name: "StockItemKind",
                schema: "dbo",
                table: "AnkInventoryReservations");

            migrationBuilder.DropColumn(
                name: "IngredientId",
                schema: "dbo",
                table: "AnkInventoryLevels");

            migrationBuilder.DropColumn(
                name: "ReorderPoint",
                schema: "dbo",
                table: "AnkInventoryLevels");

            migrationBuilder.DropColumn(
                name: "ReorderQuantity",
                schema: "dbo",
                table: "AnkInventoryLevels");

            migrationBuilder.DropColumn(
                name: "StockItemKind",
                schema: "dbo",
                table: "AnkInventoryLevels");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductVariantId",
                schema: "dbo",
                table: "AnkInventoryReservations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CartId",
                schema: "dbo",
                table: "AnkInventoryReservations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductVariantId",
                schema: "dbo",
                table: "AnkInventoryLevels",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkInventoryLevels_TenantId_ProductVariantId_Location",
                schema: "dbo",
                table: "AnkInventoryLevels",
                columns: new[] { "TenantId", "ProductVariantId", "Location" },
                unique: true,
                filter: "[Location] IS NOT NULL");
        }
    }
}

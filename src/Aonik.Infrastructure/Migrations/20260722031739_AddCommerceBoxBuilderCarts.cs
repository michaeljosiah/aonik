using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommerceBoxBuilderCarts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PersonalisationDisplayJson",
                schema: "dbo",
                table: "AnkProductionOrderLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalisationJson",
                schema: "dbo",
                table: "AnkProductionOrderLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalisationSummary",
                schema: "dbo",
                table: "AnkProductionOrderLines",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PersonalisationAdjustment",
                schema: "dbo",
                table: "AnkOrderBundleSelections",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalisationEnvelopeJson",
                schema: "dbo",
                table: "AnkOrderBundleSelections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalisationJson",
                schema: "dbo",
                table: "AnkOrderBundleSelections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalisationSummary",
                schema: "dbo",
                table: "AnkOrderBundleSelections",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitSurcharge",
                schema: "dbo",
                table: "AnkOrderBundleSelections",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BoxBundleProductId",
                schema: "dbo",
                table: "AnkCarts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BoxSize",
                schema: "dbo",
                table: "AnkCarts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BoxBundleSlotId",
                schema: "dbo",
                table: "AnkCartItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LineKind",
                schema: "dbo",
                table: "AnkCartItems",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "BoxDish");

            migrationBuilder.AddColumn<decimal>(
                name: "PersonalisationAdjustment",
                schema: "dbo",
                table: "AnkCartItems",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalisationJson",
                schema: "dbo",
                table: "AnkCartItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalisationSummary",
                schema: "dbo",
                table: "AnkCartItems",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitSurcharge",
                schema: "dbo",
                table: "AnkCartItems",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnkBundleSizePlans",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BundleProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinSize = table.Column<int>(type: "int", nullable: false),
                    MaxSize = table.Column<int>(type: "int", nullable: false),
                    BaseSize = table.Column<int>(type: "int", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    PerSpacePrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
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
                    table.PrimaryKey("PK_AnkBundleSizePlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkBundleSizePresets",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BundleSizePlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Size = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Badge = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Blurb = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SavingAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AnkBundleSizePresets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkBundleSizePresets_AnkBundleSizePlans_BundleSizePlanId",
                        column: x => x.BundleSizePlanId,
                        principalSchema: "dbo",
                        principalTable: "AnkBundleSizePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkBundleSizePlans_TenantId_BundleProductId",
                schema: "dbo",
                table: "AnkBundleSizePlans",
                columns: new[] { "TenantId", "BundleProductId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AnkBundleSizePresets_BundleSizePlanId",
                schema: "dbo",
                table: "AnkBundleSizePresets",
                column: "BundleSizePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkBundleSizePresets_TenantId_BundleSizePlanId_Size",
                schema: "dbo",
                table: "AnkBundleSizePresets",
                columns: new[] { "TenantId", "BundleSizePlanId", "Size" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkBundleSizePresets",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkBundleSizePlans",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "PersonalisationDisplayJson",
                schema: "dbo",
                table: "AnkProductionOrderLines");

            migrationBuilder.DropColumn(
                name: "PersonalisationJson",
                schema: "dbo",
                table: "AnkProductionOrderLines");

            migrationBuilder.DropColumn(
                name: "PersonalisationSummary",
                schema: "dbo",
                table: "AnkProductionOrderLines");

            migrationBuilder.DropColumn(
                name: "PersonalisationAdjustment",
                schema: "dbo",
                table: "AnkOrderBundleSelections");

            migrationBuilder.DropColumn(
                name: "PersonalisationEnvelopeJson",
                schema: "dbo",
                table: "AnkOrderBundleSelections");

            migrationBuilder.DropColumn(
                name: "PersonalisationJson",
                schema: "dbo",
                table: "AnkOrderBundleSelections");

            migrationBuilder.DropColumn(
                name: "PersonalisationSummary",
                schema: "dbo",
                table: "AnkOrderBundleSelections");

            migrationBuilder.DropColumn(
                name: "UnitSurcharge",
                schema: "dbo",
                table: "AnkOrderBundleSelections");

            migrationBuilder.DropColumn(
                name: "BoxBundleProductId",
                schema: "dbo",
                table: "AnkCarts");

            migrationBuilder.DropColumn(
                name: "BoxSize",
                schema: "dbo",
                table: "AnkCarts");

            migrationBuilder.DropColumn(
                name: "BoxBundleSlotId",
                schema: "dbo",
                table: "AnkCartItems");

            migrationBuilder.DropColumn(
                name: "LineKind",
                schema: "dbo",
                table: "AnkCartItems");

            migrationBuilder.DropColumn(
                name: "PersonalisationAdjustment",
                schema: "dbo",
                table: "AnkCartItems");

            migrationBuilder.DropColumn(
                name: "PersonalisationJson",
                schema: "dbo",
                table: "AnkCartItems");

            migrationBuilder.DropColumn(
                name: "PersonalisationSummary",
                schema: "dbo",
                table: "AnkCartItems");

            migrationBuilder.DropColumn(
                name: "UnitSurcharge",
                schema: "dbo",
                table: "AnkCartItems");
        }
    }
}

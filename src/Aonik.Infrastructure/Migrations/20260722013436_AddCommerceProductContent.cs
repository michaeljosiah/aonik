using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommerceProductContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkProductContents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServingLabel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Kcal = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    ProteinGrams = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    CarbsGrams = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    FatGrams = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    FibreGrams = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    SugarsGrams = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    SaltGrams = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    Ingredients = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Allergens = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeatingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DescribesSelectionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequiresReview = table.Column<bool>(type: "bit", nullable: false),
                    ContentVersion = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AnkProductContents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkProductContentVariants",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectionHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ServingLabel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Kcal = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    ProteinGrams = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    CarbsGrams = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    FatGrams = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    FibreGrams = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    SugarsGrams = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    SaltGrams = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    Ingredients = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Allergens = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeatingJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AnkProductContentVariants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkProductContents_TenantId_ProductId",
                schema: "dbo",
                table: "AnkProductContents",
                columns: new[] { "TenantId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkProductContentVariants_TenantId_ProductId",
                schema: "dbo",
                table: "AnkProductContentVariants",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkProductContentVariants_TenantId_ProductId_SelectionHash",
                schema: "dbo",
                table: "AnkProductContentVariants",
                columns: new[] { "TenantId", "ProductId", "SelectionHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkProductContents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkProductContentVariants",
                schema: "dbo");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDishCatalogDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComponentsLine",
                schema: "dbo",
                table: "AnkProducts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Freezable",
                schema: "dbo",
                table: "AnkProducts",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Heat",
                schema: "dbo",
                table: "AnkProducts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPlaceholder",
                schema: "dbo",
                table: "AnkProducts",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "LowSugar",
                schema: "dbo",
                table: "AnkProducts",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedCollectionId",
                schema: "dbo",
                table: "AnkProducts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShelfLife",
                schema: "dbo",
                table: "AnkProducts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AltText",
                schema: "dbo",
                table: "AnkProductMedia",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SaturatesGrams",
                schema: "dbo",
                table: "AnkProductContentVariants",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SaturatesGrams",
                schema: "dbo",
                table: "AnkProductContents",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComponentsLine",
                schema: "dbo",
                table: "AnkProducts");

            migrationBuilder.DropColumn(
                name: "Freezable",
                schema: "dbo",
                table: "AnkProducts");

            migrationBuilder.DropColumn(
                name: "Heat",
                schema: "dbo",
                table: "AnkProducts");

            migrationBuilder.DropColumn(
                name: "IsPlaceholder",
                schema: "dbo",
                table: "AnkProducts");

            migrationBuilder.DropColumn(
                name: "LowSugar",
                schema: "dbo",
                table: "AnkProducts");

            migrationBuilder.DropColumn(
                name: "RelatedCollectionId",
                schema: "dbo",
                table: "AnkProducts");

            migrationBuilder.DropColumn(
                name: "ShelfLife",
                schema: "dbo",
                table: "AnkProducts");

            migrationBuilder.DropColumn(
                name: "AltText",
                schema: "dbo",
                table: "AnkProductMedia");

            migrationBuilder.DropColumn(
                name: "SaturatesGrams",
                schema: "dbo",
                table: "AnkProductContentVariants");

            migrationBuilder.DropColumn(
                name: "SaturatesGrams",
                schema: "dbo",
                table: "AnkProductContents");
        }
    }
}

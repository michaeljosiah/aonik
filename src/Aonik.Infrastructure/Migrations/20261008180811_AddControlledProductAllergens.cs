using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddControlledProductAllergens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllergensPresentJson",
                schema: "dbo",
                table: "AnkProductContentVariants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrecautionaryStatement",
                schema: "dbo",
                table: "AnkProductContentVariants",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllergensPresentJson",
                schema: "dbo",
                table: "AnkProductContents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrecautionaryStatement",
                schema: "dbo",
                table: "AnkProductContents",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllergensPresentJson",
                schema: "dbo",
                table: "AnkProductContentVariants");

            migrationBuilder.DropColumn(
                name: "PrecautionaryStatement",
                schema: "dbo",
                table: "AnkProductContentVariants");

            migrationBuilder.DropColumn(
                name: "AllergensPresentJson",
                schema: "dbo",
                table: "AnkProductContents");

            migrationBuilder.DropColumn(
                name: "PrecautionaryStatement",
                schema: "dbo",
                table: "AnkProductContents");
        }
    }
}

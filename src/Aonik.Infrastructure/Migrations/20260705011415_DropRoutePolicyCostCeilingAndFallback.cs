using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropRoutePolicyCostCeilingAndFallback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostCeiling",
                schema: "dbo",
                table: "AnkAiRoutePolicies");

            migrationBuilder.DropColumn(
                name: "FallbackModelIdsJson",
                schema: "dbo",
                table: "AnkAiRoutePolicies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostCeiling",
                schema: "dbo",
                table: "AnkAiRoutePolicies",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FallbackModelIdsJson",
                schema: "dbo",
                table: "AnkAiRoutePolicies",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}

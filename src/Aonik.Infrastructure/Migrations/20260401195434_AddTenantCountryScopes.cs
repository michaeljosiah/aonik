using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantCountryScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedDestinationCountriesJson",
                schema: "dbo",
                table: "AnkTenants",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AllowedOriginCountriesJson",
                schema: "dbo",
                table: "AnkTenants",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedDestinationCountriesJson",
                schema: "dbo",
                table: "AnkTenants");

            migrationBuilder.DropColumn(
                name: "AllowedOriginCountriesJson",
                schema: "dbo",
                table: "AnkTenants");
        }
    }
}

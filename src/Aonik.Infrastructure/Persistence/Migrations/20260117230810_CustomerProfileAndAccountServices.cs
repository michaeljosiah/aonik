using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerProfileAndAccountServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "PersonProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "PersonProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "PersonProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "PersonProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "PersonProfiles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "PersonProfiles");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "PersonProfiles");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "PersonProfiles");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "PersonProfiles");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "PersonProfiles");
        }
    }
}

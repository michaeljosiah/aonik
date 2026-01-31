using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonProfileThumbnailUrls_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoUrlMedium",
                table: "PersonProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrlSmall",
                table: "PersonProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrlTiny",
                table: "PersonProfiles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoUrlMedium",
                table: "PersonProfiles");

            migrationBuilder.DropColumn(
                name: "PhotoUrlSmall",
                table: "PersonProfiles");

            migrationBuilder.DropColumn(
                name: "PhotoUrlTiny",
                table: "PersonProfiles");
        }
    }
}

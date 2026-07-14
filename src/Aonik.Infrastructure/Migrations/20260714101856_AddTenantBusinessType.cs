using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBusinessType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppliedPackVersion",
                schema: "dbo",
                table: "AnkTenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                schema: "dbo",
                table: "AnkTenants",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "base");

            migrationBuilder.CreateIndex(
                name: "IX_AnkTenants_BusinessType",
                schema: "dbo",
                table: "AnkTenants",
                column: "BusinessType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkTenants_BusinessType",
                schema: "dbo",
                table: "AnkTenants");

            migrationBuilder.DropColumn(
                name: "AppliedPackVersion",
                schema: "dbo",
                table: "AnkTenants");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                schema: "dbo",
                table: "AnkTenants");
        }
    }
}

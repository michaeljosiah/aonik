using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogBillerServiceCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ServiceCode",
                table: "CatalogBillerServices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBillerServices_TenantId_ServiceCode",
                table: "CatalogBillerServices",
                columns: new[] { "TenantId", "ServiceCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CatalogBillerServices_TenantId_ServiceCode",
                table: "CatalogBillerServices");

            migrationBuilder.DropColumn(
                name: "ServiceCode",
                table: "CatalogBillerServices");
        }
    }
}

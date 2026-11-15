using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCuratedConnectorPackageReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConnectorPackageJson",
                schema: "dbo",
                table: "AnkTenantMcpServers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConnectorPackageJson",
                schema: "dbo",
                table: "AnkTenantHttpTools",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectorPackageJson",
                schema: "dbo",
                table: "AnkTenantMcpServers");

            migrationBuilder.DropColumn(
                name: "ConnectorPackageJson",
                schema: "dbo",
                table: "AnkTenantHttpTools");
        }
    }
}

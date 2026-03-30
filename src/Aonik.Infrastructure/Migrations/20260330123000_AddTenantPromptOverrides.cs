using Aonik.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    [DbContext(typeof(AonikDbContext))]
    [Migration("20260330123000_AddTenantPromptOverrides")]
    public partial class AddTenantPromptOverrides : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "dbo",
                table: "AnkPromptSpecs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserTemplate",
                schema: "dbo",
                table: "AnkPromptSpecs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: string.Empty);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "dbo",
                table: "AnkPromptSpecs");

            migrationBuilder.DropColumn(
                name: "UserTemplate",
                schema: "dbo",
                table: "AnkPromptSpecs");
        }
    }
}

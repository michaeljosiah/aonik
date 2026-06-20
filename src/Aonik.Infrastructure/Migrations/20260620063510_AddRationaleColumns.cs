using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRationaleColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConditionsJson",
                schema: "dbo",
                table: "AnkUserMemoryEntries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionType",
                schema: "dbo",
                table: "AnkUserMemoryEntries",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StaleWhen",
                schema: "dbo",
                table: "AnkUserMemoryEntries",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMemoryEntries_TenantUser_DecisionType",
                schema: "dbo",
                table: "AnkUserMemoryEntries",
                columns: new[] { "TenantId", "UserId", "DecisionType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserMemoryEntries_TenantUser_DecisionType",
                schema: "dbo",
                table: "AnkUserMemoryEntries");

            migrationBuilder.DropColumn(
                name: "ConditionsJson",
                schema: "dbo",
                table: "AnkUserMemoryEntries");

            migrationBuilder.DropColumn(
                name: "DecisionType",
                schema: "dbo",
                table: "AnkUserMemoryEntries");

            migrationBuilder.DropColumn(
                name: "StaleWhen",
                schema: "dbo",
                table: "AnkUserMemoryEntries");
        }
    }
}

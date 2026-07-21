using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileUserMemoryEntrySupersededByDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserMemoryEntry_UserMemoryEntry_SupersededById",
                schema: "dbo",
                table: "AnkUserMemoryEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_UserMemoryEntry_UserMemoryEntry_SupersededById",
                schema: "dbo",
                table: "AnkUserMemoryEntries",
                column: "SupersededById",
                principalSchema: "dbo",
                principalTable: "AnkUserMemoryEntries",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserMemoryEntry_UserMemoryEntry_SupersededById",
                schema: "dbo",
                table: "AnkUserMemoryEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_UserMemoryEntry_UserMemoryEntry_SupersededById",
                schema: "dbo",
                table: "AnkUserMemoryEntries",
                column: "SupersededById",
                principalSchema: "dbo",
                principalTable: "AnkUserMemoryEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

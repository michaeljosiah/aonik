using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatRoomRunOriginIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AnkChatRoomTurns_TenantId_AiRunId",
                schema: "dbo",
                table: "AnkChatRoomTurns",
                columns: new[] { "TenantId", "AiRunId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkChatRoomTurns_TenantId_AiRunId",
                schema: "dbo",
                table: "AnkChatRoomTurns");
        }
    }
}

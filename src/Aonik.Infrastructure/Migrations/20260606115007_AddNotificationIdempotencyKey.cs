using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "dbo",
                table: "AnkNotifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Tenant_User_IdempotencyKey",
                schema: "dbo",
                table: "AnkNotifications",
                columns: new[] { "TenantId", "UserId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notification_Tenant_User_IdempotencyKey",
                schema: "dbo",
                table: "AnkNotifications");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "dbo",
                table: "AnkNotifications");
        }
    }
}

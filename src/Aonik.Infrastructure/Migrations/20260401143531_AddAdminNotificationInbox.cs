using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminNotificationInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayloadJson",
                schema: "dbo",
                table: "AnkNotifications");

            migrationBuilder.RenameColumn(
                name: "TemplateKey",
                schema: "dbo",
                table: "AnkNotifications",
                newName: "MetadataJson");

            migrationBuilder.RenameColumn(
                name: "SentAt",
                schema: "dbo",
                table: "AnkNotifications",
                newName: "ReadAt");

            migrationBuilder.RenameColumn(
                name: "RecipientRef",
                schema: "dbo",
                table: "AnkNotifications",
                newName: "Body");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkNotifications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Channel",
                schema: "dbo",
                table: "AnkNotifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "ActionUrl",
                schema: "dbo",
                table: "AnkNotifications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AiRunId",
                schema: "dbo",
                table: "AnkNotifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                schema: "dbo",
                table: "AnkNotifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DismissedAt",
                schema: "dbo",
                table: "AnkNotifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                schema: "dbo",
                table: "AnkNotifications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "dbo",
                table: "AnkNotifications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "dbo",
                table: "AnkNotifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "dbo",
                table: "AnkNotifications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                schema: "dbo",
                table: "AnkNotifications",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Tenant_User_CreatedAt",
                schema: "dbo",
                table: "AnkNotifications",
                columns: new[] { "TenantId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Tenant_User_Status_CreatedAt",
                schema: "dbo",
                table: "AnkNotifications",
                columns: new[] { "TenantId", "UserId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notification_Tenant_User_CreatedAt",
                schema: "dbo",
                table: "AnkNotifications");

            migrationBuilder.DropIndex(
                name: "IX_Notification_Tenant_User_Status_CreatedAt",
                schema: "dbo",
                table: "AnkNotifications");

            migrationBuilder.DropColumn(
                name: "ActionUrl",
                schema: "dbo",
                table: "AnkNotifications");

            migrationBuilder.DropColumn(
                name: "AiRunId",
                schema: "dbo",
                table: "AnkNotifications");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                schema: "dbo",
                table: "AnkNotifications");

            migrationBuilder.DropColumn(
                name: "DismissedAt",
                schema: "dbo",
                table: "AnkNotifications");

            migrationBuilder.DropColumn(
                name: "Severity",
                schema: "dbo",
                table: "AnkNotifications");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "dbo",
                table: "AnkNotifications");

            migrationBuilder.DropColumn(
                name: "Title",
                schema: "dbo",
                table: "AnkNotifications");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "dbo",
                table: "AnkNotifications");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "dbo",
                table: "AnkNotifications");

            migrationBuilder.RenameColumn(
                name: "ReadAt",
                schema: "dbo",
                table: "AnkNotifications",
                newName: "SentAt");

            migrationBuilder.RenameColumn(
                name: "MetadataJson",
                schema: "dbo",
                table: "AnkNotifications",
                newName: "TemplateKey");

            migrationBuilder.RenameColumn(
                name: "Body",
                schema: "dbo",
                table: "AnkNotifications",
                newName: "RecipientRef");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkNotifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Channel",
                schema: "dbo",
                table: "AnkNotifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "PayloadJson",
                schema: "dbo",
                table: "AnkNotifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}

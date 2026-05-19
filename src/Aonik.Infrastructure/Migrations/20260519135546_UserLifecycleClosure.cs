using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserLifecycleClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InviteAcceptedUtc",
                schema: "dbo",
                table: "AnkUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InviteEmailSendCount",
                schema: "dbo",
                table: "AnkUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "InviteEmailSentUtc",
                schema: "dbo",
                table: "AnkUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InviteExpiresUtc",
                schema: "dbo",
                table: "AnkUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteToken",
                schema: "dbo",
                table: "AnkUsers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnkUserInviteLogs",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SentUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TokenPrefix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnkUserInviteLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkUserSessionBlocklist",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevokedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnkUserSessionBlocklist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkUserTombstones",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeletedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MaskedEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    AuditRowsRedacted = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnkUserTombstones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_User_InviteToken",
                schema: "dbo",
                table: "AnkUsers",
                column: "InviteToken",
                unique: true,
                filter: "[InviteToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserInviteLog_TenantId_UserId_SentUtc",
                schema: "dbo",
                table: "AnkUserInviteLogs",
                columns: new[] { "TenantId", "UserId", "SentUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessionBlocklist_ExpiresUtc",
                schema: "dbo",
                table: "AnkUserSessionBlocklist",
                column: "ExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessionBlocklist_TenantId_UserId_RevokedUtc",
                schema: "dbo",
                table: "AnkUserSessionBlocklist",
                columns: new[] { "TenantId", "UserId", "RevokedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTombstones_OriginalUserId",
                schema: "dbo",
                table: "AnkUserTombstones",
                column: "OriginalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTombstones_TenantId_DeletedUtc",
                schema: "dbo",
                table: "AnkUserTombstones",
                columns: new[] { "TenantId", "DeletedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkUserInviteLogs",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkUserSessionBlocklist",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkUserTombstones",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_User_InviteToken",
                schema: "dbo",
                table: "AnkUsers");

            migrationBuilder.DropColumn(
                name: "InviteAcceptedUtc",
                schema: "dbo",
                table: "AnkUsers");

            migrationBuilder.DropColumn(
                name: "InviteEmailSendCount",
                schema: "dbo",
                table: "AnkUsers");

            migrationBuilder.DropColumn(
                name: "InviteEmailSentUtc",
                schema: "dbo",
                table: "AnkUsers");

            migrationBuilder.DropColumn(
                name: "InviteExpiresUtc",
                schema: "dbo",
                table: "AnkUsers");

            migrationBuilder.DropColumn(
                name: "InviteToken",
                schema: "dbo",
                table: "AnkUsers");
        }
    }
}

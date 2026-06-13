using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCircleSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkCircleGrants",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Scope = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EntityIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NoAmounts = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
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
                    table.PrimaryKey("PK_AnkCircleGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkCircleInvites",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EntityIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NoAmounts = table.Column<bool>(type: "bit", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GrantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AnkCircleInvites", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkCircleGrants_TenantId_MemberUserId_Status",
                schema: "dbo",
                table: "AnkCircleGrants",
                columns: new[] { "TenantId", "MemberUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkCircleGrants_TenantId_OwnerUserId_MemberUserId_Status",
                schema: "dbo",
                table: "AnkCircleGrants",
                columns: new[] { "TenantId", "OwnerUserId", "MemberUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkCircleInvites_TenantId_OwnerUserId_Status",
                schema: "dbo",
                table: "AnkCircleInvites",
                columns: new[] { "TenantId", "OwnerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkCircleInvites_TenantId_Token",
                schema: "dbo",
                table: "AnkCircleInvites",
                columns: new[] { "TenantId", "Token" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkCircleGrants",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkCircleInvites",
                schema: "dbo");
        }
    }
}

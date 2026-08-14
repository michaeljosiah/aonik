using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEntitlementTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkEntitlementRevocations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Jti = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevocationHandle = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SweepAfter = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AnkEntitlementRevocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkEntitlementSigningKeys",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kid = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Algorithm = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PublicKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProtectedPrivateKey = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    NotBefore = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SigningNotAfter = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifyNotAfter = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AnkEntitlementSigningKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkEntitlementTokenIssues",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriberKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SubscriberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Jti = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kid = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RevocationHandle = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DeviceFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GraceUntil = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AnkEntitlementTokenIssues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkEntitlementRevocations_TenantId_SweepAfter",
                schema: "dbo",
                table: "AnkEntitlementRevocations",
                columns: new[] { "TenantId", "SweepAfter" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkEntitlementSigningKeys_TenantId_Kid",
                schema: "dbo",
                table: "AnkEntitlementSigningKeys",
                columns: new[] { "TenantId", "Kid" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AnkEntitlementTokenIssues_TenantId_Jti",
                schema: "dbo",
                table: "AnkEntitlementTokenIssues",
                columns: new[] { "TenantId", "Jti" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AnkEntitlementTokenIssues_TenantId_Kid_GraceUntil",
                schema: "dbo",
                table: "AnkEntitlementTokenIssues",
                columns: new[] { "TenantId", "Kid", "GraceUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkEntitlementTokenIssues_TenantId_SubscriberKind_SubscriberId",
                schema: "dbo",
                table: "AnkEntitlementTokenIssues",
                columns: new[] { "TenantId", "SubscriberKind", "SubscriberId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkEntitlementRevocations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkEntitlementSigningKeys",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkEntitlementTokenIssues",
                schema: "dbo");
        }
    }
}

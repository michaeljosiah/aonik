using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceQuotaAndShareAccessLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BillingSubscriberId",
                schema: "dbo",
                table: "AnkWorkspaces",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "BillingSubscriberKind",
                schema: "dbo",
                table: "AnkWorkspaces",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AccessLevel",
                schema: "dbo",
                table: "AnkCircleGrants",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<long>(
                name: "Held",
                schema: "dbo",
                table: "AnkCeilingHoldings",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<long>(
                name: "Weight",
                schema: "dbo",
                table: "AnkCeilingClaims",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "AnkBlobPossessions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriberKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SubscriberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    WorkspaceCount = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AnkBlobPossessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkBlobPossessions_TenantId_SubscriberKind_SubscriberId_ContentHash",
                schema: "dbo",
                table: "AnkBlobPossessions",
                columns: new[] { "TenantId", "SubscriberKind", "SubscriberId", "ContentHash" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkBlobPossessions",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "BillingSubscriberId",
                schema: "dbo",
                table: "AnkWorkspaces");

            migrationBuilder.DropColumn(
                name: "BillingSubscriberKind",
                schema: "dbo",
                table: "AnkWorkspaces");

            migrationBuilder.DropColumn(
                name: "AccessLevel",
                schema: "dbo",
                table: "AnkCircleGrants");

            migrationBuilder.DropColumn(
                name: "Weight",
                schema: "dbo",
                table: "AnkCeilingClaims");

            migrationBuilder.AlterColumn<int>(
                name: "Held",
                schema: "dbo",
                table: "AnkCeilingHoldings",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}

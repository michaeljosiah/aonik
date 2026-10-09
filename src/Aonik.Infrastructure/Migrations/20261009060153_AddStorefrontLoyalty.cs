using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStorefrontLoyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LoyaltyJson",
                schema: "dbo",
                table: "AnkOrderChargeSummaries",
                type: "nvarchar(max)",
                maxLength: 262144,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PointsAppliedValue",
                schema: "dbo",
                table: "AnkOrderChargeSummaries",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "AnkLoyaltyAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LedgerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LiabilityAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HighestFivePoundMarkSeen = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_AnkLoyaltyAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkLoyaltyCheckoutAttempts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentIntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReservedPoints = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
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
                    table.PrimaryKey("PK_AnkLoyaltyCheckoutAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkLoyaltyOperations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JournalEntryLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Points = table.Column<long>(type: "bigint", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: false),
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
                    table.PrimaryKey("PK_AnkLoyaltyOperations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkLoyaltyAccounts_TenantId_PartyId",
                schema: "dbo",
                table: "AnkLoyaltyAccounts",
                columns: new[] { "TenantId", "PartyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkLoyaltyCheckoutAttempts_TenantId_AccountId_Status",
                schema: "dbo",
                table: "AnkLoyaltyCheckoutAttempts",
                columns: new[] { "TenantId", "AccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkLoyaltyCheckoutAttempts_TenantId_OrderId",
                schema: "dbo",
                table: "AnkLoyaltyCheckoutAttempts",
                columns: new[] { "TenantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkLoyaltyCheckoutAttempts_TenantId_PaymentIntentId",
                schema: "dbo",
                table: "AnkLoyaltyCheckoutAttempts",
                columns: new[] { "TenantId", "PaymentIntentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkLoyaltyOperations_TenantId_AccountId_OccurredAtUtc",
                schema: "dbo",
                table: "AnkLoyaltyOperations",
                columns: new[] { "TenantId", "AccountId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkLoyaltyOperations_TenantId_JournalEntryLineId",
                schema: "dbo",
                table: "AnkLoyaltyOperations",
                columns: new[] { "TenantId", "JournalEntryLineId" },
                unique: true,
                filter: "[JournalEntryLineId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkLoyaltyOperations_TenantId_Kind_SourceId",
                schema: "dbo",
                table: "AnkLoyaltyOperations",
                columns: new[] { "TenantId", "Kind", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkLoyaltyOperations_TenantId_OrderId",
                schema: "dbo",
                table: "AnkLoyaltyOperations",
                columns: new[] { "TenantId", "OrderId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkLoyaltyAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkLoyaltyCheckoutAttempts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkLoyaltyOperations",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "LoyaltyJson",
                schema: "dbo",
                table: "AnkOrderChargeSummaries");

            migrationBuilder.DropColumn(
                name: "PointsAppliedValue",
                schema: "dbo",
                table: "AnkOrderChargeSummaries");
        }
    }
}

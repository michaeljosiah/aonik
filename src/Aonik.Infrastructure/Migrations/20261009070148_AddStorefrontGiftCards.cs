using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStorefrontGiftCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GiftCardJson",
                schema: "dbo",
                table: "AnkOrderChargeSummaries",
                type: "nvarchar(max)",
                maxLength: 262144,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GiftCardPurchaseJson",
                schema: "dbo",
                table: "AnkCarts",
                type: "nvarchar(max)",
                maxLength: 12000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GiftCardTenderJson",
                schema: "dbo",
                table: "AnkCarts",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnkGiftCardCheckoutAttempts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentIntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GiftCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReservedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_AnkGiftCardCheckoutAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkGiftCardOperations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GiftCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AnkGiftCardOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkGiftCards",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentIntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemIndex = table.Column<int>(type: "int", nullable: false),
                    FaceValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PolicySnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32768, nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProtectedCode = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    MaskedCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AnkGiftCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkOrderGiftCardDeliveries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentIntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemIndex = table.Column<int>(type: "int", nullable: false),
                    PurchaseSnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    DeliveryMethod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SendAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GiftCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaskedCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SendSequence = table.Column<int>(type: "int", nullable: false),
                    SentSequence = table.Column<int>(type: "int", nullable: false),
                    SequenceDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastResendRequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResendWindowStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResendsInWindow = table.Column<int>(type: "int", nullable: false),
                    LastPrintedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPrintedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AnkOrderGiftCardDeliveries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCardCheckoutAttempts_TenantId_GiftCardId_Status",
                schema: "dbo",
                table: "AnkGiftCardCheckoutAttempts",
                columns: new[] { "TenantId", "GiftCardId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCardCheckoutAttempts_TenantId_OrderId",
                schema: "dbo",
                table: "AnkGiftCardCheckoutAttempts",
                columns: new[] { "TenantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCardCheckoutAttempts_TenantId_PaymentIntentId",
                schema: "dbo",
                table: "AnkGiftCardCheckoutAttempts",
                columns: new[] { "TenantId", "PaymentIntentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCardOperations_TenantId_GiftCardId_OccurredAtUtc",
                schema: "dbo",
                table: "AnkGiftCardOperations",
                columns: new[] { "TenantId", "GiftCardId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCardOperations_TenantId_JournalEntryLineId",
                schema: "dbo",
                table: "AnkGiftCardOperations",
                columns: new[] { "TenantId", "JournalEntryLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCardOperations_TenantId_Kind_SourceId",
                schema: "dbo",
                table: "AnkGiftCardOperations",
                columns: new[] { "TenantId", "Kind", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCards_TenantId_CartId",
                schema: "dbo",
                table: "AnkGiftCards",
                columns: new[] { "TenantId", "CartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCards_TenantId_CodeHash",
                schema: "dbo",
                table: "AnkGiftCards",
                columns: new[] { "TenantId", "CodeHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCards_TenantId_OrderItemId",
                schema: "dbo",
                table: "AnkGiftCards",
                columns: new[] { "TenantId", "OrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkGiftCards_TenantId_PaymentIntentId",
                schema: "dbo",
                table: "AnkGiftCards",
                columns: new[] { "TenantId", "PaymentIntentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrderGiftCardDeliveries_TenantId_DeliveryMethod_PostingDate_Status",
                schema: "dbo",
                table: "AnkOrderGiftCardDeliveries",
                columns: new[] { "TenantId", "DeliveryMethod", "PostingDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrderGiftCardDeliveries_TenantId_OrderId_OrderItemId",
                schema: "dbo",
                table: "AnkOrderGiftCardDeliveries",
                columns: new[] { "TenantId", "OrderId", "OrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrderGiftCardDeliveries_TenantId_PaymentIntentId",
                schema: "dbo",
                table: "AnkOrderGiftCardDeliveries",
                columns: new[] { "TenantId", "PaymentIntentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkGiftCardCheckoutAttempts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkGiftCardOperations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkGiftCards",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkOrderGiftCardDeliveries",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "GiftCardJson",
                schema: "dbo",
                table: "AnkOrderChargeSummaries");

            migrationBuilder.DropColumn(
                name: "GiftCardPurchaseJson",
                schema: "dbo",
                table: "AnkCarts");

            migrationBuilder.DropColumn(
                name: "GiftCardTenderJson",
                schema: "dbo",
                table: "AnkCarts");
        }
    }
}

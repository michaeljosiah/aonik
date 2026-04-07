using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommitmentFieldsAndPersonalRecurringBill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Autopay",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConfidenceScore",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Frequency",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Monthly");

            migrationBuilder.AddColumn<decimal>(
                name: "LastChargedAmount",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastChargedAt",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastObservedAt",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LinkedBillId",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<Guid>(
                name: "PaidFromAccountId",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceTransactionId",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubCategory",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Confirmed");

            migrationBuilder.CreateTable(
                name: "AnkDebtRepayments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaidFromAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreditorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DebtType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NextDueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Monthly"),
                    Autopay = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Active"),
                    VerificationStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Confirmed"),
                    Origin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Manual"),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    SourceTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AccountReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastObservedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPaidAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
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
                    table.PrimaryKey("PK_AnkDebtRepayments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkPersonalRecurringBills",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaidFromAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Payee = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Monthly"),
                    NextDueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Autopay = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Active"),
                    VerificationStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Confirmed"),
                    Origin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Manual"),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    SourceTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DetectionSource = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PayeeReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReminderDaysBefore = table.Column<int>(type: "int", nullable: true),
                    GracePeriodDays = table.Column<int>(type: "int", nullable: true),
                    LastObservedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPaidAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
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
                    table.PrimaryKey("PK_AnkPersonalRecurringBills", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSubscriptions_TenantId_UserId_SourceTransactionId",
                schema: "dbo",
                table: "AnkSubscriptions",
                columns: new[] { "TenantId", "UserId", "SourceTransactionId" },
                filter: "[SourceTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkSubscriptions_TenantId_UserId_Status",
                schema: "dbo",
                table: "AnkSubscriptions",
                columns: new[] { "TenantId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSubscriptions_TenantId_UserId_VerificationStatus",
                schema: "dbo",
                table: "AnkSubscriptions",
                columns: new[] { "TenantId", "UserId", "VerificationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkDebtRepayments_TenantId_UserId",
                schema: "dbo",
                table: "AnkDebtRepayments",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkDebtRepayments_TenantId_UserId_NextDueDate",
                schema: "dbo",
                table: "AnkDebtRepayments",
                columns: new[] { "TenantId", "UserId", "NextDueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkDebtRepayments_TenantId_UserId_SourceTransactionId",
                schema: "dbo",
                table: "AnkDebtRepayments",
                columns: new[] { "TenantId", "UserId", "SourceTransactionId" },
                filter: "[SourceTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkDebtRepayments_TenantId_UserId_Status",
                schema: "dbo",
                table: "AnkDebtRepayments",
                columns: new[] { "TenantId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkDebtRepayments_TenantId_UserId_VerificationStatus",
                schema: "dbo",
                table: "AnkDebtRepayments",
                columns: new[] { "TenantId", "UserId", "VerificationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPersonalRecurringBills_TenantId_UserId",
                schema: "dbo",
                table: "AnkPersonalRecurringBills",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPersonalRecurringBills_TenantId_UserId_NextDueDate",
                schema: "dbo",
                table: "AnkPersonalRecurringBills",
                columns: new[] { "TenantId", "UserId", "NextDueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPersonalRecurringBills_TenantId_UserId_SourceTransactionId",
                schema: "dbo",
                table: "AnkPersonalRecurringBills",
                columns: new[] { "TenantId", "UserId", "SourceTransactionId" },
                filter: "[SourceTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPersonalRecurringBills_TenantId_UserId_Status",
                schema: "dbo",
                table: "AnkPersonalRecurringBills",
                columns: new[] { "TenantId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPersonalRecurringBills_TenantId_UserId_VerificationStatus",
                schema: "dbo",
                table: "AnkPersonalRecurringBills",
                columns: new[] { "TenantId", "UserId", "VerificationStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkDebtRepayments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkPersonalRecurringBills",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_AnkSubscriptions_TenantId_UserId_SourceTransactionId",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_AnkSubscriptions_TenantId_UserId_Status",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_AnkSubscriptions_TenantId_UserId_VerificationStatus",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropColumn(
                name: "Autopay",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropColumn(
                name: "ConfidenceScore",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropColumn(
                name: "Frequency",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropColumn(
                name: "LastChargedAmount",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropColumn(
                name: "LastChargedAt",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropColumn(
                name: "LastObservedAt",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropColumn(
                name: "LinkedBillId",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropColumn(
                name: "Origin",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropColumn(
                name: "PaidFromAccountId",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropColumn(
                name: "SourceTransactionId",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropColumn(
                name: "SubCategory",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                schema: "dbo",
                table: "AnkSubscriptions");
        }
    }
}

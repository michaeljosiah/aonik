using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Merchant",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedAmount",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "DetectedBy",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "BalanceAsOf",
                schema: "dbo",
                table: "AnkPersonalAccounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentBalance",
                schema: "dbo",
                table: "AnkPersonalAccounts",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "TargetAmount",
                schema: "dbo",
                table: "AnkGoals",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkGoals",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ProgressAmount",
                schema: "dbo",
                table: "AnkGoals",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "dbo",
                table: "AnkGoals",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "AnkGoals",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkBudgets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PeriodType",
                schema: "dbo",
                table: "AnkBudgets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "BudgetCreatedBy",
                schema: "dbo",
                table: "AnkBudgets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<decimal>(
                name: "LimitAmount",
                schema: "dbo",
                table: "AnkBudgetLines",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "AnkBudgetLines",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                schema: "dbo",
                table: "AnkBudgetLines",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Payee",
                schema: "dbo",
                table: "AnkBills",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Frequency",
                schema: "dbo",
                table: "AnkBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedAmount",
                schema: "dbo",
                table: "AnkBills",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "AnkBills",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "TransactionAttachments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageProvider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StorageContainer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_TransactionAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionAttachments_AnkPersonalTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalSchema: "dbo",
                        principalTable: "AnkPersonalTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSubscriptions_TenantId_UserId",
                schema: "dbo",
                table: "AnkSubscriptions",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSubscriptions_TenantId_UserId_RenewalDate",
                schema: "dbo",
                table: "AnkSubscriptions",
                columns: new[] { "TenantId", "UserId", "RenewalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkGoals_TenantId_UserId",
                schema: "dbo",
                table: "AnkGoals",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkGoals_TenantId_UserId_Status",
                schema: "dbo",
                table: "AnkGoals",
                columns: new[] { "TenantId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkBudgets_TenantId_UserId",
                schema: "dbo",
                table: "AnkBudgets",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkBudgets_TenantId_UserId_PeriodStart_Status",
                schema: "dbo",
                table: "AnkBudgets",
                columns: new[] { "TenantId", "UserId", "PeriodStart", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkBills_TenantId_UserId",
                schema: "dbo",
                table: "AnkBills",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkBills_TenantId_UserId_NextDueDate",
                schema: "dbo",
                table: "AnkBills",
                columns: new[] { "TenantId", "UserId", "NextDueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkBills_TenantId_UserId_Status",
                schema: "dbo",
                table: "AnkBills",
                columns: new[] { "TenantId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAttachments_TenantId_TransactionId",
                schema: "dbo",
                table: "TransactionAttachments",
                columns: new[] { "TenantId", "TransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAttachments_TransactionId",
                schema: "dbo",
                table: "TransactionAttachments",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionAttachments",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_AnkSubscriptions_TenantId_UserId",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_AnkSubscriptions_TenantId_UserId_RenewalDate",
                schema: "dbo",
                table: "AnkSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_AnkGoals_TenantId_UserId",
                schema: "dbo",
                table: "AnkGoals");

            migrationBuilder.DropIndex(
                name: "IX_AnkGoals_TenantId_UserId_Status",
                schema: "dbo",
                table: "AnkGoals");

            migrationBuilder.DropIndex(
                name: "IX_AnkBudgets_TenantId_UserId",
                schema: "dbo",
                table: "AnkBudgets");

            migrationBuilder.DropIndex(
                name: "IX_AnkBudgets_TenantId_UserId_PeriodStart_Status",
                schema: "dbo",
                table: "AnkBudgets");

            migrationBuilder.DropIndex(
                name: "IX_AnkBills_TenantId_UserId",
                schema: "dbo",
                table: "AnkBills");

            migrationBuilder.DropIndex(
                name: "IX_AnkBills_TenantId_UserId_NextDueDate",
                schema: "dbo",
                table: "AnkBills");

            migrationBuilder.DropIndex(
                name: "IX_AnkBills_TenantId_UserId_Status",
                schema: "dbo",
                table: "AnkBills");

            migrationBuilder.DropColumn(
                name: "BalanceAsOf",
                schema: "dbo",
                table: "AnkPersonalAccounts");

            migrationBuilder.DropColumn(
                name: "CurrentBalance",
                schema: "dbo",
                table: "AnkPersonalAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Merchant",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedAmount",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.AlterColumn<string>(
                name: "DetectedBy",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "AnkSubscriptions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "TargetAmount",
                schema: "dbo",
                table: "AnkGoals",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkGoals",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<decimal>(
                name: "ProgressAmount",
                schema: "dbo",
                table: "AnkGoals",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "dbo",
                table: "AnkGoals",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "AnkGoals",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkBudgets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "PeriodType",
                schema: "dbo",
                table: "AnkBudgets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "BudgetCreatedBy",
                schema: "dbo",
                table: "AnkBudgets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<decimal>(
                name: "LimitAmount",
                schema: "dbo",
                table: "AnkBudgetLines",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "AnkBudgetLines",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                schema: "dbo",
                table: "AnkBudgetLines",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkBills",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Payee",
                schema: "dbo",
                table: "AnkBills",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Frequency",
                schema: "dbo",
                table: "AnkBills",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedAmount",
                schema: "dbo",
                table: "AnkBills",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "AnkBills",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);
        }
    }
}

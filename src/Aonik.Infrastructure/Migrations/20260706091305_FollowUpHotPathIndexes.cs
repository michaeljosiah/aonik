using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FollowUpHotPathIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkLedgerAccounts_Code",
                schema: "dbo",
                table: "AnkLedgerAccounts");

            migrationBuilder.DropIndex(
                name: "IX_AnkLedgerAccounts_Name",
                schema: "dbo",
                table: "AnkLedgerAccounts");

            migrationBuilder.DropIndex(
                name: "IX_AnkJournalEntries_Timestamp",
                schema: "dbo",
                table: "AnkJournalEntries");

            migrationBuilder.CreateIndex(
                name: "IX_AnkLedgerAccounts_TenantId_Code",
                schema: "dbo",
                table: "AnkLedgerAccounts",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkLedgerAccounts_TenantId_Name",
                schema: "dbo",
                table: "AnkLedgerAccounts",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkJournalEntries_TenantId_Timestamp",
                schema: "dbo",
                table: "AnkJournalEntries",
                columns: new[] { "TenantId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkInvoices_TenantId_IssueDate_Id",
                schema: "dbo",
                table: "AnkInvoices",
                columns: new[] { "TenantId", "IssueDate", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkLedgerAccounts_TenantId_Code",
                schema: "dbo",
                table: "AnkLedgerAccounts");

            migrationBuilder.DropIndex(
                name: "IX_AnkLedgerAccounts_TenantId_Name",
                schema: "dbo",
                table: "AnkLedgerAccounts");

            migrationBuilder.DropIndex(
                name: "IX_AnkJournalEntries_TenantId_Timestamp",
                schema: "dbo",
                table: "AnkJournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_AnkInvoices_TenantId_IssueDate_Id",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.CreateIndex(
                name: "IX_AnkLedgerAccounts_Code",
                schema: "dbo",
                table: "AnkLedgerAccounts",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_AnkLedgerAccounts_Name",
                schema: "dbo",
                table: "AnkLedgerAccounts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AnkJournalEntries_Timestamp",
                schema: "dbo",
                table: "AnkJournalEntries",
                column: "Timestamp");
        }
    }
}

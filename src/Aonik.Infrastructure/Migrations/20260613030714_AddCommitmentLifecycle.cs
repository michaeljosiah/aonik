using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommitmentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnchorDay",
                schema: "dbo",
                table: "AnkPersonalRecurringBills",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CareEntityId",
                schema: "dbo",
                table: "AnkPersonalRecurringBills",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommitmentKind",
                schema: "dbo",
                table: "AnkPersonalRecurringBills",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Bill");

            migrationBuilder.AddColumn<int>(
                name: "RhythmInterval",
                schema: "dbo",
                table: "AnkPersonalRecurringBills",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "RhythmUnit",
                schema: "dbo",
                table: "AnkPersonalRecurringBills",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Monthly");

            migrationBuilder.AddColumn<string>(
                name: "TermDatesJson",
                schema: "dbo",
                table: "AnkPersonalRecurringBills",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnkCommitmentCycles",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PaymentLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SkipReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SnoozedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AnkCommitmentCycles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPersonalRecurringBills_TenantId_UserId_CareEntityId",
                schema: "dbo",
                table: "AnkPersonalRecurringBills",
                columns: new[] { "TenantId", "UserId", "CareEntityId" },
                filter: "[CareEntityId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkCommitmentCycles_TenantId_CommitmentId_Status",
                schema: "dbo",
                table: "AnkCommitmentCycles",
                columns: new[] { "TenantId", "CommitmentId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkCommitmentCycles",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_AnkPersonalRecurringBills_TenantId_UserId_CareEntityId",
                schema: "dbo",
                table: "AnkPersonalRecurringBills");

            migrationBuilder.DropColumn(
                name: "AnchorDay",
                schema: "dbo",
                table: "AnkPersonalRecurringBills");

            migrationBuilder.DropColumn(
                name: "CareEntityId",
                schema: "dbo",
                table: "AnkPersonalRecurringBills");

            migrationBuilder.DropColumn(
                name: "CommitmentKind",
                schema: "dbo",
                table: "AnkPersonalRecurringBills");

            migrationBuilder.DropColumn(
                name: "RhythmInterval",
                schema: "dbo",
                table: "AnkPersonalRecurringBills");

            migrationBuilder.DropColumn(
                name: "RhythmUnit",
                schema: "dbo",
                table: "AnkPersonalRecurringBills");

            migrationBuilder.DropColumn(
                name: "TermDatesJson",
                schema: "dbo",
                table: "AnkPersonalRecurringBills");
        }
    }
}

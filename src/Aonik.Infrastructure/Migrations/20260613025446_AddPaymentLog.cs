using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkPaymentLogs",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CareEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CommitmentCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ApproxGbp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SourceTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorroborationStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AnkPaymentLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentLogs_SourceTransactionId",
                schema: "dbo",
                table: "AnkPaymentLogs",
                column: "SourceTransactionId",
                unique: true,
                filter: "[SourceTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentLogs_TenantId_UserId_CareEntityId_Date",
                schema: "dbo",
                table: "AnkPaymentLogs",
                columns: new[] { "TenantId", "UserId", "CareEntityId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentLogs_TenantId_UserId_CommitmentId",
                schema: "dbo",
                table: "AnkPaymentLogs",
                columns: new[] { "TenantId", "UserId", "CommitmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentLogs_TenantId_UserId_IdempotencyKey",
                schema: "dbo",
                table: "AnkPaymentLogs",
                columns: new[] { "TenantId", "UserId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkPaymentLogs",
                schema: "dbo");
        }
    }
}

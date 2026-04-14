using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatThreadSummaryAttemptTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SummaryAttemptCount",
                schema: "dbo",
                table: "AnkChatThreads",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SummaryLastAttemptedAt",
                schema: "dbo",
                table: "AnkChatThreads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_Status_LastMessageAt_SummaryAttemptCount",
                schema: "dbo",
                table: "AnkChatThreads",
                columns: new[] { "Status", "LastMessageAt", "SummaryAttemptCount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatThreads_Status_LastMessageAt_SummaryAttemptCount",
                schema: "dbo",
                table: "AnkChatThreads");

            migrationBuilder.DropColumn(
                name: "SummaryAttemptCount",
                schema: "dbo",
                table: "AnkChatThreads");

            migrationBuilder.DropColumn(
                name: "SummaryLastAttemptedAt",
                schema: "dbo",
                table: "AnkChatThreads");
        }
    }
}

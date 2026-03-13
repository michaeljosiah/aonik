using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialConnectionRecurringSyncFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoSyncEnabled",
                schema: "dbo",
                table: "AnkFinancialConnections",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastWebhookReceivedAt",
                schema: "dbo",
                table: "AnkFinancialConnections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextScheduledSyncAt",
                schema: "dbo",
                table: "AnkFinancialConnections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SyncIntervalMinutes",
                schema: "dbo",
                table: "AnkFinancialConnections",
                type: "int",
                nullable: false,
                defaultValue: 360);

            migrationBuilder.CreateIndex(
                name: "IX_AnkFinancialConnections_AutoSyncEnabled_NextScheduledSyncAt",
                schema: "dbo",
                table: "AnkFinancialConnections",
                columns: new[] { "AutoSyncEnabled", "NextScheduledSyncAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkFinancialConnections_AutoSyncEnabled_NextScheduledSyncAt",
                schema: "dbo",
                table: "AnkFinancialConnections");

            migrationBuilder.DropColumn(
                name: "AutoSyncEnabled",
                schema: "dbo",
                table: "AnkFinancialConnections");

            migrationBuilder.DropColumn(
                name: "LastWebhookReceivedAt",
                schema: "dbo",
                table: "AnkFinancialConnections");

            migrationBuilder.DropColumn(
                name: "NextScheduledSyncAt",
                schema: "dbo",
                table: "AnkFinancialConnections");

            migrationBuilder.DropColumn(
                name: "SyncIntervalMinutes",
                schema: "dbo",
                table: "AnkFinancialConnections");
        }
    }
}

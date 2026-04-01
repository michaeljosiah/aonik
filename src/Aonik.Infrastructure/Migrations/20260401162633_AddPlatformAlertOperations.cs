using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAlertOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkAzureMonitorAlertEvents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExternalAlertId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AlertRuleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AlertRuleId = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    MonitorCondition = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignalType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MonitoringService = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NormalizedType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ResourceIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EssentialsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AlertContextJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomPropertiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnalysisSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnalysisJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AiRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingAttempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AnkAzureMonitorAlertEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AzureMonitorAlertEvent_CorrelationKey_ReceivedAtUtc",
                schema: "dbo",
                table: "AnkAzureMonitorAlertEvents",
                columns: new[] { "CorrelationKey", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AzureMonitorAlertEvent_ExternalAlertId",
                schema: "dbo",
                table: "AnkAzureMonitorAlertEvents",
                column: "ExternalAlertId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AzureMonitorAlertEvent_Status_ReceivedAtUtc",
                schema: "dbo",
                table: "AnkAzureMonitorAlertEvents",
                columns: new[] { "Status", "ReceivedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkAzureMonitorAlertEvents",
                schema: "dbo");
        }
    }
}
